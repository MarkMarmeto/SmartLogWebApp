using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartLog.Web.Services;

namespace SmartLog.Web.Controllers.Api;

[ApiController]
[Route("api/v1/dashboard")]
[Authorize]
public class DashboardApiController : ControllerBase
{
    private readonly IDashboardService _dashboardService;

    public DashboardApiController(IDashboardService dashboardService)
    {
        _dashboardService = dashboardService;
    }

    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary()
    {
        var summary = await _dashboardService.GetSummaryAsync();
        return Ok(summary);
    }

    [HttpGet("attendance-trend")]
    public async Task<IActionResult> GetAttendanceTrend([FromQuery] int days = 30)
    {
        if (days < 1 || days > 365)
            days = 30;

        var trend = await _dashboardService.GetAttendanceTrendAsync(days);
        return Ok(trend);
    }

    [HttpGet("attendance-by-grade")]
    public async Task<IActionResult> GetAttendanceByGrade([FromQuery] DateTime? date = null)
    {
        var targetDate = date ?? DateTime.UtcNow.Date;
        var data = await _dashboardService.GetAttendanceByGradeAsync(targetDate);
        return Ok(data);
    }

    [HttpGet("attendance-by-weekday")]
    public async Task<IActionResult> GetAttendanceByWeekday([FromQuery] int weeks = 4)
    {
        if (weeks < 1 || weeks > 52)
            weeks = 4;

        var data = await _dashboardService.GetAttendanceByWeekdayAsync(weeks);
        return Ok(data);
    }

    [HttpGet("recent-activity")]
    public async Task<IActionResult> GetRecentActivity([FromQuery] int count = 10)
    {
        if (count < 1 || count > 50)
            count = 10;

        var activities = await _dashboardService.GetRecentActivityAsync(count);
        return Ok(activities);
    }
}
