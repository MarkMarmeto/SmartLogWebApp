using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using QRCoder;
using SmartLog.Web.Data;
using SmartLog.Web.Data.Entities;

namespace SmartLog.Web.Services;

/// <summary>
/// Service for managing visitor passes: generation, activation/deactivation, QR signing.
/// Implements US0072 (Visitor Pass Entity & QR Generation).
/// </summary>
public class VisitorPassService : IVisitorPassService
{
    private readonly ApplicationDbContext _context;
    private readonly IAppSettingsService _appSettingsService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<VisitorPassService> _logger;

    private static readonly SemaphoreSlim _generateLock = new(1, 1);

    public VisitorPassService(
        ApplicationDbContext context,
        IAppSettingsService appSettingsService,
        IConfiguration configuration,
        ILogger<VisitorPassService> logger)
    {
        _context = context;
        _appSettingsService = appSettingsService;
        _configuration = configuration;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<List<VisitorPass>> GeneratePassesAsync()
    {
        await _generateLock.WaitAsync();
        try
        {
            var maxPasses = await GetMaxPassesAsync();
            var existingCount = await _context.VisitorPasses.CountAsync();

            if (existingCount >= maxPasses)
            {
                _logger.LogInformation("All {Count} visitor passes already generated", existingCount);
                return await _context.VisitorPasses.OrderBy(p => p.PassNumber).ToListAsync();
            }

            var secretKey = await GetSecretKeyAsync();
            var newPasses = new List<VisitorPass>();

            for (var i = existingCount + 1; i <= maxPasses; i++)
            {
                var code = $"VISITOR-{i:D3}";
                var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                var dataToSign = $"{code}:{timestamp}";
                var hmacSignature = ComputeHmac(dataToSign, secretKey);
                var payload = $"SMARTLOG-V:{code}:{timestamp}:{hmacSignature}";
                var qrImageBase64 = GenerateQrImage(payload);

                var pass = new VisitorPass
                {
                    Id = Guid.NewGuid(),
                    PassNumber = i,
                    Code = code,
                    QrPayload = payload,
                    HmacSignature = hmacSignature,
                    QrImageBase64 = qrImageBase64,
                    IsActive = true,
                    IssuedAt = DateTime.UtcNow,
                    CurrentStatus = "Available"
                };

                newPasses.Add(pass);
            }

            _context.VisitorPasses.AddRange(newPasses);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Generated {Count} new visitor passes (VISITOR-{Start:D3} to VISITOR-{End:D3})",
                newPasses.Count, existingCount + 1, maxPasses);

            if (maxPasses > 100)
            {
                _logger.LogWarning("Large visitor pass count: {Count}. Consider if this many passes are needed.", maxPasses);
            }

            return await _context.VisitorPasses.OrderBy(p => p.PassNumber).ToListAsync();
        }
        finally
        {
            _generateLock.Release();
        }
    }

    /// <inheritdoc />
    public async Task<VisitorPass?> GetByCodeAsync(string code)
    {
        return await _context.VisitorPasses.FirstOrDefaultAsync(p => p.Code == code);
    }

    /// <inheritdoc />
    public async Task<List<VisitorPass>> GetAllAsync()
    {
        return await _context.VisitorPasses.OrderBy(p => p.PassNumber).ToListAsync();
    }

    /// <inheritdoc />
    public async Task DeactivatePassAsync(Guid passId)
    {
        var pass = await _context.VisitorPasses.FindAsync(passId)
            ?? throw new InvalidOperationException($"Visitor pass {passId} not found");

        pass.IsActive = false;
        pass.CurrentStatus = "Deactivated";
        await _context.SaveChangesAsync();

        _logger.LogInformation("Deactivated visitor pass {Code}", pass.Code);
    }

    /// <inheritdoc />
    public async Task ActivatePassAsync(Guid passId)
    {
        var pass = await _context.VisitorPasses.FindAsync(passId)
            ?? throw new InvalidOperationException($"Visitor pass {passId} not found");

        pass.IsActive = true;
        pass.CurrentStatus = "Available";
        await _context.SaveChangesAsync();

        _logger.LogInformation("Activated visitor pass {Code}", pass.Code);
    }

    /// <inheritdoc />
    public async Task SyncPassCountAsync()
    {
        var maxPasses = await GetMaxPassesAsync();
        var existingCount = await _context.VisitorPasses.CountAsync();

        if (existingCount < maxPasses)
        {
            await GeneratePassesAsync();
        }
        else if (existingCount > maxPasses)
        {
            // Deactivate highest-numbered excess passes
            var excessPasses = await _context.VisitorPasses
                .Where(p => p.PassNumber > maxPasses)
                .ToListAsync();

            foreach (var pass in excessPasses)
            {
                pass.IsActive = false;
                pass.CurrentStatus = "Deactivated";
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation("Deactivated {Count} excess visitor passes (numbers > {Max})",
                excessPasses.Count, maxPasses);
        }
    }

    /// <inheritdoc />
    public async Task<int> GetMaxPassesAsync()
    {
        return await _appSettingsService.GetAsync("Visitor:MaxPasses", 20);
    }

    /// <inheritdoc />
    public async Task SetMaxPassesAsync(int count)
    {
        if (count < 1)
            throw new ArgumentException("Maximum passes must be at least 1", nameof(count));

        await _appSettingsService.SetAsync(
            "Visitor:MaxPasses",
            count.ToString(),
            "Visitor",
            updatedBy: null,
            description: "Maximum number of visitor passes to generate");

        _logger.LogInformation("Visitor max passes updated to {Count}", count);
    }

    private async Task<string> GetSecretKeyAsync()
    {
        // Same key resolution as QrCodeService — shared HMAC secret
        var key = await _appSettingsService.GetAsync("QRCode.HmacSecretKey");
        if (!string.IsNullOrEmpty(key) && !key.StartsWith("${"))
            return key;

        var envKey = Environment.GetEnvironmentVariable("SMARTLOG_HMAC_SECRET_KEY");
        if (!string.IsNullOrEmpty(envKey))
            return envKey;

        var configKey = _configuration["QrCode:HmacSecretKey"];
        if (!string.IsNullOrEmpty(configKey) && !configKey.StartsWith("${"))
            return configKey;

        throw new InvalidOperationException(
            "HMAC secret key not configured. Set SMARTLOG_HMAC_SECRET_KEY environment variable or update in Admin Settings.");
    }

    /// <inheritdoc />
    public async Task<VisitorLogResult> GetVisitorLogAsync(DateTime? startDate, DateTime? endDate, string? passCodeFilter, int page, int pageSize, string? deviceFilter = null, string? cameraFilter = null)
    {
        // Query ENTRY scans as the base for visit pairing
        var entryQuery = _context.VisitorScans
            .Include(s => s.VisitorPass)
            .Include(s => s.Device)
            .Where(s => s.ScanType == "ENTRY" && s.Status == "ACCEPTED");

        if (startDate.HasValue)
            entryQuery = entryQuery.Where(s => s.ScannedAt >= startDate.Value.Date);
        if (endDate.HasValue)
            entryQuery = entryQuery.Where(s => s.ScannedAt < endDate.Value.Date.AddDays(1));
        if (!string.IsNullOrWhiteSpace(passCodeFilter))
            entryQuery = entryQuery.Where(s => s.VisitorPass!.Code.Contains(passCodeFilter));

        if (Guid.TryParse(deviceFilter, out var deviceGuid))
            entryQuery = entryQuery.Where(s => s.DeviceId == deviceGuid);

        if (!string.IsNullOrWhiteSpace(cameraFilter))
        {
            if (cameraFilter == "unknown")
            {
                entryQuery = entryQuery.Where(s => s.CameraIndex == null);
            }
            else
            {
                var parts = cameraFilter.Split('|', 2);
                if (int.TryParse(parts[0], out var idx))
                {
                    var name = parts.Length > 1 && parts[1].Length > 0 ? parts[1] : null;
                    entryQuery = entryQuery.Where(s => s.CameraIndex == idx && s.CameraName == name);
                }
            }
        }

        var totalCount = await entryQuery.CountAsync();

        var entryScans = await entryQuery
            .OrderByDescending(s => s.ScannedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        // Load matching EXIT scans for pairing
        var passIds = entryScans.Select(s => s.VisitorPassId).Distinct().ToList();
        var earliestEntry = entryScans.Any() ? entryScans.Min(s => s.ScannedAt) : DateTime.UtcNow;

        var exitScans = await _context.VisitorScans
            .Where(s => s.ScanType == "EXIT" && s.Status == "ACCEPTED"
                && passIds.Contains(s.VisitorPassId)
                && s.ScannedAt >= earliestEntry)
            .OrderBy(s => s.ScannedAt)
            .ToListAsync();

        // Pair each ENTRY with the nearest subsequent EXIT for the same pass
        var visits = new List<VisitorVisit>();
        foreach (var entry in entryScans)
        {
            var matchingExit = exitScans
                .FirstOrDefault(e => e.VisitorPassId == entry.VisitorPassId && e.ScannedAt > entry.ScannedAt);

            string duration;
            string status;
            if (matchingExit != null)
            {
                var span = matchingExit.ScannedAt - entry.ScannedAt;
                duration = FormatDuration(span);
                status = "Completed";
                // Remove used exit so it's not matched again
                exitScans.Remove(matchingExit);
            }
            else
            {
                duration = "In progress";
                status = "In progress";
            }

            visits.Add(new VisitorVisit
            {
                PassCode = entry.VisitorPass?.Code ?? "Unknown",
                PassNumber = entry.VisitorPass?.PassNumber ?? 0,
                EntryTime = entry.ScannedAt,
                ExitTime = matchingExit?.ScannedAt,
                Duration = duration,
                DeviceName = entry.Device?.Name ?? "Unknown",
                Status = status,
                CameraIndex = entry.CameraIndex,
                CameraName = entry.CameraName
            });
        }

        // Summary statistics (today)
        var today = DateTime.UtcNow.Date;
        var todayEntries = await _context.VisitorScans
            .Where(s => s.ScanType == "ENTRY" && s.Status == "ACCEPTED" && s.ScannedAt >= today)
            .CountAsync();

        var currentlyIn = await _context.VisitorPasses
            .CountAsync(p => p.IsActive && p.CurrentStatus == "InUse");

        // Average duration of completed visits today
        var todayExitScans = await _context.VisitorScans
            .Where(s => s.ScanType == "EXIT" && s.Status == "ACCEPTED" && s.ScannedAt >= today)
            .Select(s => new { s.VisitorPassId, s.ScannedAt })
            .ToListAsync();

        var todayEntryScans = await _context.VisitorScans
            .Where(s => s.ScanType == "ENTRY" && s.Status == "ACCEPTED" && s.ScannedAt >= today)
            .Select(s => new { s.VisitorPassId, s.ScannedAt })
            .OrderBy(s => s.ScannedAt)
            .ToListAsync();

        var durations = new List<TimeSpan>();
        var usedExits = new HashSet<int>();
        foreach (var e in todayEntryScans)
        {
            for (var i = 0; i < todayExitScans.Count; i++)
            {
                if (!usedExits.Contains(i) && todayExitScans[i].VisitorPassId == e.VisitorPassId && todayExitScans[i].ScannedAt > e.ScannedAt)
                {
                    durations.Add(todayExitScans[i].ScannedAt - e.ScannedAt);
                    usedExits.Add(i);
                    break;
                }
            }
        }

        var avgDuration = durations.Count > 0
            ? FormatDuration(TimeSpan.FromMinutes(durations.Average(d => d.TotalMinutes)))
            : "—";

        return new VisitorLogResult
        {
            Visits = visits,
            TotalCount = totalCount,
            Summary = new VisitorLogSummary
            {
                TotalVisitors = todayEntries,
                AvgDuration = avgDuration,
                CurrentlyIn = currentlyIn
            }
        };
    }

    private static string FormatDuration(TimeSpan span)
    {
        if (span.TotalHours >= 1)
            return $"{(int)span.TotalHours}h {span.Minutes}m";
        return $"{(int)span.TotalMinutes}m";
    }

    private static string ComputeHmac(string data, string secretKey)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secretKey));
        var hashBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
        return Convert.ToBase64String(hashBytes);
    }

    private static string GenerateQrImage(string payload)
    {
        using var qrGenerator = new QRCodeGenerator();
        using var qrCodeData = qrGenerator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.Q);
        using var qrCode = new PngByteQRCode(qrCodeData);

        var qrCodeBytes = qrCode.GetGraphic(20);
        return Convert.ToBase64String(qrCodeBytes);
    }
}
