using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SmartLog.Web.Data.Entities;
using SmartLog.Web.Services;

namespace SmartLog.Web.Pages.Admin.Calendar;

[Authorize(Policy = "RequireAdmin")]
public class IndexModel : PageModel
{
    private readonly ICalendarService _calendarService;
    private readonly IAcademicYearService _academicYearService;

    public IndexModel(ICalendarService calendarService, IAcademicYearService academicYearService)
    {
        _calendarService = calendarService;
        _academicYearService = academicYearService;
    }

    public List<CalendarEvent> Events { get; set; } = new();
    public int CurrentYear { get; set; }
    public int CurrentMonth { get; set; }
    public Guid? AcademicYearId { get; set; }
    public AcademicYear? CurrentAcademicYear { get; set; }
    public List<AcademicYear> AcademicYears { get; set; } = new();

    [TempData]
    public string? StatusMessage { get; set; }

    public async Task<IActionResult> OnGetAsync(int? year, int? month, Guid? academicYearId)
    {
        // Get current academic year
        CurrentAcademicYear = await _academicYearService.GetCurrentAcademicYearAsync();
        AcademicYears = await _academicYearService.GetAllAcademicYearsAsync();

        // Use current academic year if not specified
        AcademicYearId = academicYearId ?? CurrentAcademicYear?.Id;

        // Use current date if not specified
        var now = DateTime.Now;
        CurrentYear = year ?? now.Year;
        CurrentMonth = month ?? now.Month;

        // Get events for the month
        Events = await _calendarService.GetEventsForMonthAsync(CurrentYear, CurrentMonth, AcademicYearId);

        return Page();
    }

    public IActionResult OnPostPreviousMonth(int year, int month, Guid? academicYearId)
    {
        var date = new DateTime(year, month, 1).AddMonths(-1);
        return RedirectToPage(new { year = date.Year, month = date.Month, academicYearId });
    }

    public IActionResult OnPostNextMonth(int year, int month, Guid? academicYearId)
    {
        var date = new DateTime(year, month, 1).AddMonths(1);
        return RedirectToPage(new { year = date.Year, month = date.Month, academicYearId });
    }
}
