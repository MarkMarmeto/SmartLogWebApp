using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using SmartLog.Web.Data.Entities;
using SmartLog.Web.Services;

namespace SmartLog.Web.Pages.Admin.Calendar;

[Authorize(Policy = "RequireAdmin")]
public class CreateEventModel : PageModel
{
    private readonly ICalendarService _calendarService;
    private readonly IAcademicYearService _academicYearService;
    private readonly UserManager<ApplicationUser> _userManager;

    public CreateEventModel(
        ICalendarService calendarService,
        IAcademicYearService academicYearService,
        UserManager<ApplicationUser> userManager)
    {
        _calendarService = calendarService;
        _academicYearService = academicYearService;
        _userManager = userManager;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public List<SelectListItem> AcademicYears { get; set; } = new();
    public List<SelectListItem> EventTypes { get; set; } = new();
    public List<SelectListItem> HolidayCategories { get; set; } = new();
    public List<SelectListItem> EventCategories { get; set; } = new();
    public List<SelectListItem> SuspensionCategories { get; set; } = new();

    [TempData]
    public string? StatusMessage { get; set; }

    public class InputModel
    {
        [Required]
        [Display(Name = "Title")]
        [StringLength(200)]
        public string Title { get; set; } = string.Empty;

        [Display(Name = "Description")]
        [StringLength(1000)]
        public string? Description { get; set; }

        [Required]
        [Display(Name = "Event Type")]
        public EventType EventType { get; set; }

        [Required]
        [Display(Name = "Category")]
        public string Category { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Start Date")]
        public DateTime StartDate { get; set; } = DateTime.Today;

        [Required]
        [Display(Name = "End Date")]
        public DateTime EndDate { get; set; } = DateTime.Today;

        [Display(Name = "All Day Event")]
        public bool IsAllDay { get; set; } = true;

        [Display(Name = "Affects Attendance")]
        public bool AffectsAttendance { get; set; } = true;

        [Display(Name = "Affects Classes")]
        public bool AffectsClasses { get; set; }

        [Display(Name = "Location")]
        [StringLength(200)]
        public string? Location { get; set; }

        [Required]
        [Display(Name = "Academic Year")]
        public Guid AcademicYearId { get; set; }
    }

    public async Task<IActionResult> OnGetAsync(Guid? academicYearId)
    {
        await PopulateDropdownsAsync();

        // Set default academic year
        var currentAcademicYear = await _academicYearService.GetCurrentAcademicYearAsync();
        Input.AcademicYearId = academicYearId ?? currentAcademicYear?.Id ?? Guid.Empty;
        Input.EventType = EventType.Holiday;
        Input.Category = "National";

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            await PopulateDropdownsAsync();
            return Page();
        }

        // Validate dates
        if (Input.EndDate < Input.StartDate)
        {
            ModelState.AddModelError("Input.EndDate", "End date must be after or equal to start date.");
            await PopulateDropdownsAsync();
            return Page();
        }

        try
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                ModelState.AddModelError(string.Empty, "User not found.");
                await PopulateDropdownsAsync();
                return Page();
            }

            var calendarEvent = new CalendarEvent
            {
                Title = Input.Title,
                Description = Input.Description,
                EventType = Input.EventType,
                Category = Input.Category,
                StartDate = Input.StartDate,
                EndDate = Input.EndDate,
                IsAllDay = Input.IsAllDay,
                AffectsAttendance = Input.AffectsAttendance,
                AffectsClasses = Input.AffectsClasses,
                Location = Input.Location,
                AcademicYearId = Input.AcademicYearId,
                CreatedBy = user.Id,
                IsActive = true
            };

            await _calendarService.CreateEventAsync(calendarEvent);

            StatusMessage = $"{Input.EventType} '{Input.Title}' created successfully.";
            return RedirectToPage("./Index", new { academicYearId = Input.AcademicYearId });
        }
        catch (Exception ex)
        {
            ModelState.AddModelError(string.Empty, $"Error creating event: {ex.Message}");
            await PopulateDropdownsAsync();
            return Page();
        }
    }

    private async Task PopulateDropdownsAsync()
    {
        var academicYears = await _academicYearService.GetAllAcademicYearsAsync();
        AcademicYears = academicYears.Select(ay => new SelectListItem
        {
            Value = ay.Id.ToString(),
            Text = ay.Name,
            Selected = ay.IsCurrent
        }).ToList();

        EventTypes = Enum.GetValues(typeof(EventType))
            .Cast<EventType>()
            .Select(et => new SelectListItem
            {
                Value = ((int)et).ToString(),
                Text = et.ToString()
            }).ToList();

        HolidayCategories = new List<SelectListItem>
        {
            new() { Value = "National", Text = "National Holiday" },
            new() { Value = "Regional", Text = "Regional Holiday" },
            new() { Value = "School", Text = "School Holiday" },
            new() { Value = "Religious", Text = "Religious Holiday" }
        };

        EventCategories = new List<SelectListItem>
        {
            new() { Value = "Meeting", Text = "Meeting" },
            new() { Value = "Program", Text = "School Program" },
            new() { Value = "SportsDay", Text = "Sports Day" },
            new() { Value = "FieldTrip", Text = "Field Trip" },
            new() { Value = "Assembly", Text = "Assembly" },
            new() { Value = "Exam", Text = "Examination" },
            new() { Value = "ParentTeacher", Text = "Parent-Teacher Conference" }
        };

        SuspensionCategories = new List<SelectListItem>
        {
            new() { Value = "Weather", Text = "Weather/Typhoon" },
            new() { Value = "Emergency", Text = "Emergency" },
            new() { Value = "Training", Text = "Teacher Training" },
            new() { Value = "Government", Text = "Government Order" }
        };
    }
}
