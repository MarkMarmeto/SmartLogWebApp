using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SmartLog.Web.Data.Entities;
using SmartLog.Web.Services;

namespace SmartLog.Web.Pages.Admin.Calendar;

[Authorize(Policy = "RequireAdmin")]
public class EventDetailsModel : PageModel
{
    private readonly ICalendarService _calendarService;

    public EventDetailsModel(ICalendarService calendarService)
    {
        _calendarService = calendarService;
    }

    public CalendarEvent CalendarEvent { get; set; } = null!;

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        var calendarEvent = await _calendarService.GetEventByIdAsync(id);
        if (calendarEvent == null)
        {
            return NotFound();
        }

        CalendarEvent = calendarEvent;
        return Page();
    }
}
