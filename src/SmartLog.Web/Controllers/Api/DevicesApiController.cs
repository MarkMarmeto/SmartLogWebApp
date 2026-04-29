using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartLog.Web.Data;
using SmartLog.Web.Services;

namespace SmartLog.Web.Controllers.Api;

/// <summary>
/// US0119 AC7: Admin API for device health summary. Cookie-auth, SuperAdmin only.
/// </summary>
[ApiController]
[Route("api/v1/devices")]
[Authorize(Policy = "RequireSuperAdmin")]
public class DevicesApiController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IDeviceHealthService _healthService;

    public DevicesApiController(ApplicationDbContext context, IDeviceHealthService healthService)
    {
        _context = context;
        _healthService = healthService;
    }

    [HttpGet("health")]
    public async Task<IActionResult> GetHealth()
    {
        var thresholds = await _healthService.GetThresholdsAsync();
        var now = DateTime.UtcNow;
        var devices = await _context.Devices
            .Where(d => d.IsActive)
            .OrderBy(d => d.Name)
            .Select(d => new
            {
                d.Id, d.Name, d.LastSeenAt, d.LastHeartbeatAt,
                d.AppVersion, d.OsVersion, d.NetworkType,
                d.BatteryPercent, d.IsCharging, d.QueuedScansCount
            })
            .ToListAsync();

        var result = devices.Select(d => new
        {
            deviceId = d.Id,
            name = d.Name,
            status = DeviceHealthService.ComputeStatusInternal(d.LastSeenAt, now, thresholds).ToString(),
            lastSeenAt = d.LastSeenAt,
            lastHeartbeatAt = d.LastHeartbeatAt,
            appVersion = d.AppVersion,
            osVersion = d.OsVersion,
            networkType = d.NetworkType,
            batteryPercent = d.BatteryPercent,
            isCharging = d.IsCharging,
            queuedScansCount = d.QueuedScansCount
        });

        return Ok(result);
    }
}
