using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartLog.Web.Data;
using SmartLog.Web.Services;
using System.Diagnostics;

namespace SmartLog.Web.Controllers.Api;

/// <summary>
/// Health check endpoint for scanner devices.
/// Implements US0033 (Health Check Endpoint).
/// </summary>
[ApiController]
[Route("api/v1/health")]
[Produces("application/json")]
public class HealthController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IDeviceService _deviceService;
    private readonly ILogger<HealthController> _logger;

    public HealthController(ApplicationDbContext context, IDeviceService deviceService, ILogger<HealthController> logger)
    {
        _context = context;
        _deviceService = deviceService;
        _logger = logger;
    }

    /// <summary>
    /// Returns the server's current UTC time. Used by scanner devices for clock synchronization.
    /// No authentication required — this is a lightweight, read-only endpoint.
    /// </summary>
    [HttpGet("time")]
    public IActionResult GetServerTime()
    {
        return Ok(new { utc = DateTime.UtcNow.ToString("o") });
    }

    /// <summary>
    /// Basic health check endpoint (unauthenticated).
    /// Returns 200 OK if the service is healthy.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Get()
    {
        try
        {
            // Check database connectivity
            await _context.Database.CanConnectAsync();

            return Ok(new HealthResponse
            {
                Status = "healthy",
                Timestamp = DateTime.UtcNow,
                Version = "1.0.0"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Health check failed");

            return StatusCode(503, new HealthResponse
            {
                Status = "unhealthy",
                Timestamp = DateTime.UtcNow,
                Version = "1.0.0",
                Error = "Database connectivity issue"
            });
        }
    }

    /// <summary>
    /// Detailed health check endpoint (authenticated via X-API-Key).
    /// Used by scanner setup wizard to validate API key and server connectivity.
    /// </summary>
    [HttpGet("details")]
    public async Task<IActionResult> GetDetails()
    {
        // Authenticate via X-API-Key header
        if (!Request.Headers.TryGetValue("X-API-Key", out var apiKeyHeader) ||
            string.IsNullOrWhiteSpace(apiKeyHeader))
        {
            return Unauthorized(new ErrorResponse
            {
                Error = "InvalidApiKey",
                Message = "Invalid or missing API key"
            });
        }

        var apiKey = apiKeyHeader.ToString();
        var keyHash = _deviceService.HashApiKey(apiKey);
        var device = await _context.Devices.FirstOrDefaultAsync(d => d.ApiKeyHash == keyHash);

        if (device == null)
        {
            _logger.LogWarning("Invalid API key attempt on health/details from {IpAddress}",
                HttpContext.Connection.RemoteIpAddress);
            return Unauthorized(new ErrorResponse
            {
                Error = "InvalidApiKey",
                Message = "Invalid or missing API key"
            });
        }

        // Update device last seen
        device.LastSeenAt = DateTime.UtcNow;

        try
        {
            // Measure database connectivity latency
            var stopwatch = Stopwatch.StartNew();
            await _context.Database.CanConnectAsync();
            stopwatch.Stop();

            // Count active scanners
            var activeScanners = await _context.Devices.CountAsync(d => d.IsActive);

            // Count scans today (UTC)
            var todayUtc = DateTime.UtcNow.Date;
            var tomorrowUtc = todayUtc.AddDays(1);
            var scansToday = await _context.Scans
                .CountAsync(s => s.ScannedAt >= todayUtc && s.ScannedAt < tomorrowUtc);

            await _context.SaveChangesAsync();

            return Ok(new HealthDetailsResponse
            {
                Status = "healthy",
                Timestamp = DateTime.UtcNow,
                Version = "1.0.0",
                Database = new DatabaseHealth
                {
                    Status = "healthy",
                    LatencyMs = stopwatch.ElapsedMilliseconds
                },
                ActiveScanners = activeScanners,
                ScansToday = scansToday
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Detailed health check failed");

            try { await _context.SaveChangesAsync(); } catch { /* best effort to save LastSeenAt */ }

            return StatusCode(503, new HealthDetailsResponse
            {
                Status = "unhealthy",
                Timestamp = DateTime.UtcNow,
                Version = "1.0.0",
                Database = new DatabaseHealth
                {
                    Status = "unhealthy",
                    LatencyMs = -1
                },
                Error = "Database connectivity issue"
            });
        }
    }
}

/// <summary>
/// Basic health check response model.
/// </summary>
public class HealthResponse
{
    public string Status { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string Version { get; set; } = string.Empty;
    public string? Error { get; set; }
}

/// <summary>
/// Detailed health check response model (authenticated endpoint).
/// </summary>
public class HealthDetailsResponse
{
    public string Status { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string Version { get; set; } = string.Empty;
    public DatabaseHealth Database { get; set; } = new();
    public int ActiveScanners { get; set; }
    public int ScansToday { get; set; }
    public string? Error { get; set; }
}

/// <summary>
/// Database health status.
/// </summary>
public class DatabaseHealth
{
    public string Status { get; set; } = string.Empty;
    public long LatencyMs { get; set; }
}
