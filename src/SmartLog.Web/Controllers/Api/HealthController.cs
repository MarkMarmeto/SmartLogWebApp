using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using SmartLog.Web.Data;
using SmartLog.Web.Data.Entities;
using SmartLog.Web.Services;
using System.Diagnostics;
using System.Globalization;
using System.Text.Json.Serialization;

namespace SmartLog.Web.Controllers.Api;

/// <summary>
/// Unified health endpoint for scanner devices and ops tooling.
/// Implements US0033 (original /health) and US0121 (auth-aware unification).
///
/// Three response states:
///   - No / empty X-API-Key  -> 200 OK, minimal liveness payload, no DB hit.
///   - Valid X-API-Key       -> 200 OK (or 503), full payload, updates Device.LastSeenAt.
///   - Present-but-invalid   -> 401 InvalidApiKey.
/// </summary>
[ApiController]
[Route("api/v1/health")]
[Produces("application/json")]
public class HealthController : ControllerBase
{
    private const string Version = "1.0.0";
    private const string InvalidKeyLogPrefix = "health:invalid-key:";
    private static readonly TimeSpan InvalidKeyLogWindow = TimeSpan.FromMinutes(5);

    private readonly ApplicationDbContext _context;
    private readonly IDeviceService _deviceService;
    private readonly IMemoryCache _memoryCache;
    private readonly ILogger<HealthController> _logger;

    public HealthController(
        ApplicationDbContext context,
        IDeviceService deviceService,
        IMemoryCache memoryCache,
        ILogger<HealthController> logger)
    {
        _context = context;
        _deviceService = deviceService;
        _memoryCache = memoryCache;
        _logger = logger;
    }

    /// <summary>
    /// Unified health endpoint. Response detail level adapts to the X-API-Key header
    /// (see class summary).
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Get()
    {
        Response.Headers.CacheControl = "no-cache, no-store";

        var response = new HealthResponse
        {
            Status = "healthy",
            ServerTime = FormatServerTime(DateTime.UtcNow),
            Version = Version
        };

        // Unauthenticated path — no DB query. Critical for fleet-scale polling.
        if (!Request.Headers.TryGetValue("X-API-Key", out var headerVal)
            || string.IsNullOrWhiteSpace(headerVal))
        {
            return Ok(response);
        }

        // Authenticated path — validate the key.
        Device? device;
        try
        {
            var keyHash = _deviceService.HashApiKey(headerVal!);
            device = await _context.Devices.FirstOrDefaultAsync(d => d.ApiKeyHash == keyHash);
        }
        catch (Exception ex)
        {
            // DB is down before we can even validate the key. The caller provided a key,
            // so we owe them an authenticated-style 503 (with serverTime/version) — not
            // an unauth 200 that would mask the outage.
            _logger.LogError(ex, "Health auth: DB unreachable while validating API key");
            return UnhealthyDatabase(response);
        }

        if (device is null)
        {
            LogInvalidKeyOnce();
            return Unauthorized(new ErrorResponse
            {
                Error = "InvalidApiKey",
                Message = "Invalid or missing API key"
            });
        }

        return await PopulateAuthenticatedAsync(response, device);
    }

    /// <summary>
    /// Deprecated. Use GET /api/v1/health with X-API-Key.
    /// Retained as a shim so pre-US0132 scanner builds continue to work; will be
    /// removed after the scanner client rollout completes.
    /// </summary>
    [HttpGet("details")]
    [Obsolete("Use GET /api/v1/health with X-API-Key. Removed after US0132 rollout.")]
    public Task<IActionResult> GetDetails() => Get();

    /// <summary>
    /// Deprecated. Use GET /api/v1/health — `serverTime` is now in every response.
    /// Retained as a shim so pre-US0132 scanner builds continue to work; will be
    /// removed after the scanner client rollout completes.
    /// </summary>
    [HttpGet("time")]
    [Obsolete("Use GET /api/v1/health. Removed after US0132 rollout.")]
    public IActionResult GetServerTime()
    {
        Response.Headers.CacheControl = "no-cache, no-store";
        return Ok(new { utc = FormatServerTime(DateTime.UtcNow) });
    }

    private async Task<IActionResult> PopulateAuthenticatedAsync(HealthResponse response, Device device)
    {
        // Preserve prior /health/details side-effect.
        device.LastSeenAt = DateTime.UtcNow;

        try
        {
            var sw = Stopwatch.StartNew();
            await _context.Database.CanConnectAsync();
            sw.Stop();

            var activeScanners = await _context.Devices.CountAsync(d => d.IsActive);
            var todayUtc = DateTime.UtcNow.Date;
            var tomorrowUtc = todayUtc.AddDays(1);
            var scansToday = await _context.Scans
                .CountAsync(s => s.ScannedAt >= todayUtc && s.ScannedAt < tomorrowUtc);

            await _context.SaveChangesAsync();

            response.Database = new DatabaseHealth { Status = "healthy", LatencyMs = sw.ElapsedMilliseconds };
            response.Scanners = new ScannerStats { Active = activeScanners, ScansToday = scansToday };
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Authenticated health check failed");

            // Best-effort to persist LastSeenAt even when downstream queries failed.
            try { await _context.SaveChangesAsync(); } catch { /* swallow */ }

            return UnhealthyDatabase(response);
        }
    }

    private IActionResult UnhealthyDatabase(HealthResponse response)
    {
        response.Status = "unhealthy";
        response.Error = "Database connectivity issue";
        response.Database = new DatabaseHealth { Status = "unhealthy", LatencyMs = -1 };
        return StatusCode(503, response);
    }

    private void LogInvalidKeyOnce()
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var cacheKey = InvalidKeyLogPrefix + ip;
        if (_memoryCache.TryGetValue(cacheKey, out _))
        {
            return;
        }

        _memoryCache.Set(cacheKey, true, InvalidKeyLogWindow);
        _logger.LogWarning("Invalid API key attempt on /api/v1/health from {IpAddress}", ip);
    }

    private static string FormatServerTime(DateTime utc) =>
        utc.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture);
}

/// <summary>
/// Unified health-check response. Auth-only fields use JsonIgnoreCondition.WhenWritingNull
/// so the unauthenticated payload omits them entirely.
/// </summary>
public class HealthResponse
{
    public string Status { get; set; } = string.Empty;

    /// <summary>ISO-8601 UTC, millisecond precision, trailing Z (e.g. "2026-05-08T03:14:15.926Z").</summary>
    public string ServerTime { get; set; } = string.Empty;

    public string Version { get; set; } = string.Empty;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Error { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DatabaseHealth? Database { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ScannerStats? Scanners { get; set; }
}

public class DatabaseHealth
{
    public string Status { get; set; } = string.Empty;
    public long LatencyMs { get; set; }
}

public class ScannerStats
{
    public int Active { get; set; }
    public int ScansToday { get; set; }
}
