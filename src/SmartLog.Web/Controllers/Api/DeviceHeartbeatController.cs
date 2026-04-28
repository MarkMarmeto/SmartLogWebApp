using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartLog.Web.Data;
using SmartLog.Web.Services;
using System.ComponentModel.DataAnnotations;

namespace SmartLog.Web.Controllers.Api;

/// <summary>
/// US0119 AC1, AC2, AC9, AC10: Scanner heartbeat ingestion.
/// Accepts periodic snapshots from registered scanners; updates Device row in place.
/// </summary>
[ApiController]
[Route("api/v1/devices/heartbeat")]
[Produces("application/json")]
[EnableCors("ScannerDevices")]
public class DeviceHeartbeatController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IDeviceService _deviceService;
    private readonly ILogger<DeviceHeartbeatController> _logger;

    public DeviceHeartbeatController(
        ApplicationDbContext context,
        IDeviceService deviceService,
        ILogger<DeviceHeartbeatController> logger)
    {
        _context = context;
        _deviceService = deviceService;
        _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> Post([FromBody] HeartbeatRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        if (!Request.Headers.TryGetValue("X-API-Key", out var apiKeyHeader) ||
            string.IsNullOrWhiteSpace(apiKeyHeader))
        {
            return Unauthorized(new { Error = "InvalidApiKey", Message = "Invalid or missing API key" });
        }

        var keyHash = _deviceService.HashApiKey(apiKeyHeader.ToString());
        var device = await _context.Devices.FirstOrDefaultAsync(d => d.ApiKeyHash == keyHash);

        if (device == null)
        {
            _logger.LogWarning("Invalid API key on heartbeat from {IpAddress}", HttpContext.Connection.RemoteIpAddress);
            return Unauthorized(new { Error = "InvalidApiKey", Message = "Invalid or missing API key" });
        }

        if (!device.IsActive)
        {
            return Unauthorized(new { Error = "DeviceRevoked", Message = "Device has been revoked" });
        }

        var now = DateTime.UtcNow;
        device.LastSeenAt = now;
        device.LastHeartbeatAt = now;
        device.AppVersion = Truncate(request.AppVersion, 50);
        device.OsVersion = Truncate(request.OsVersion, 100);
        device.BatteryPercent = ClampBattery(request.BatteryPercent);
        device.IsCharging = request.IsCharging;
        device.NetworkType = Truncate(request.NetworkType, 20);
        device.QueuedScansCount = request.QueuedScansCount is < 0 ? 0 : request.QueuedScansCount;

        await _context.SaveChangesAsync();
        return NoContent();
    }

    private static string? Truncate(string? value, int max)
        => value is null ? null : value.Length <= max ? value : value[..max];

    private static int? ClampBattery(int? value)
        => value is null ? null : Math.Clamp(value.Value, 0, 100);
}

public class HeartbeatRequest
{
    [StringLength(50)] public string? AppVersion { get; set; }
    [StringLength(100)] public string? OsVersion { get; set; }
    [Range(0, 100)] public int? BatteryPercent { get; set; }
    public bool? IsCharging { get; set; }
    [StringLength(20)] public string? NetworkType { get; set; }
    public DateTime? LastScanAt { get; set; }
    [Range(0, int.MaxValue)] public int? QueuedScansCount { get; set; }
    public DateTime? ClientTimestamp { get; set; }
}
