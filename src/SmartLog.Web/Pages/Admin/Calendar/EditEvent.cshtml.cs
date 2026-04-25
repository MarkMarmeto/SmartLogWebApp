using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using SmartLog.Web.Data.Entities;
using SmartLog.Web.Services;

namespace SmartLog.Web.Pages.Admin.Calendar;

[Authorize(Policy = "RequireAdmin")]
public class EditEventModel : PageModel
{
    private readonly ICalendarService _calendarService;
    private readonly IAcademicYearService _academicYearService;
    private readonly UserManager<ApplicationUser> _userManager;

    public EditEventModel(
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
        public Guid Id { get; set; }

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
        public DateTime StartDate { get; set; }

        [Required]
        [Display(Name = "End Date")]
        public DateTime EndDate { get; set; }

        [Display(Name = "All Day Event")]
        public bool IsAllDay { get; set; } = true;

        [Display(Name = "Affects Attendance")]
        public bool AffectsAttendance { get; set; }

        [Display(Name = "Affects Classes")]
        public bool AffectsClasses { get; set; }

        [Display(Name = "Location")]
        [StringLength(200)]
        public string? Location { get; set; }

        [Display(Name = "Suppress No-Scan Alert")]
        public bool SuppressesNoScanAlert { get; set; }

        [Required]
        [Display(Name = "Academic Year")]
        public Guid AcademicYearId { get; set; }

        [Display(Name = "Active")]
        public bool IsActive { get; set; } = true;
    }

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        var calendarEvent = await _calendarService.GetEventByIdAsync(id);
        if (calendarEvent == null)
        {
            return NotFound();
        }

        Input = new InputModel
        {
            Id = calendarEvent.Id,
            Title = calendarEvent.Title,
            Description = calendarEvent.Description,
            EventType = calendarEvent.EventType,
            Category = calendarEvent.Category,
            StartDate = calendarEvent.StartDate,
            EndDate = calendarEvent.EndDate,
            IsAllDay = calendarEvent.IsAllDay,
            AffectsAttendance = calendarEvent.AffectsAttendance,
            AffectsClasses = calendarEvent.AffectsClasses,
            Location = calendarEvent.Location,
            SuppressesNoScanAlert = calendarEvent.SuppressesNoScanAlert ?? false,
            AcademicYearId = calendarEvent.AcademicYearId,
            IsActive = calendarEvent.IsActive
        };

        await PopulateDropdownsAsync();
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
            var existingEvent = await _calendarService.GetEventByIdAsync(Input.Id);
            if (existingEvent == null)
            {
                return NotFound();
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                ModelState.AddModelError(string.Empty, "User not found.");
                await PopulateDropdownsAsync();
                return Page();
            }

            // Update the event
            existingEvent.Title = Input.Title;
            existingEvent.Description = Input.Description;
            existingEvent.EventType = Input.EventType;
            existingEvent.Category = Input.Category;
            existingEvent.StartDate = Input.StartDate;
            existingEvent.EndDate = Input.EndDate;
            existingEvent.IsAllDay = Input.IsAllDay;
            existingEvent.AffectsAttendance = Input.AffectsAttendance;
            existingEvent.AffectsClasses = Input.AffectsClasses;
            existingEvent.Location = Input.Location;
            existingEvent.SuppressesNoScanAlert = Input.EventType == EventType.Event ? Input.SuppressesNoScanAlert : null;
            existingEvent.AcademicYearId = Input.AcademicYearId;
            existingEvent.IsActive = Input.IsActive;
            existingEvent.UpdatedBy = user.Id;

            await _calendarService.UpdateEventAsync(existingEvent);

            StatusMessage = $"{Input.EventType} '{Input.Title}' updated successfully.";
            return RedirectToPage("./Index", new { academicYearId = Input.AcademicYearId });
        }
        catch (Exception ex)
        {
            ModelState.AddModelError(string.Empty, $"Error updating event: {ex.Message}");
            await PopulateDropdownsAsync();
            return Page();
        }
    }

    public async Task<IActionResult> OnPostDeleteAsync(Guid id)
    {
        try
        {
            var calendarEvent = await _calendarService.GetEventByIdAsync(id);
            if (calendarEvent == null)
            {
                return NotFound();
            }

            await _calendarService.DeleteEventAsync(id);

            StatusMessage = $"{calendarEvent.EventType} '{calendarEvent.Title}' deleted successfully.";
            return RedirectToPage("./Index", new { academicYearId = calendarEvent.AcademicYearId });
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error deleting event: {ex.Message}";
            return RedirectToPage("./Index");
        }
    }

    private async Task PopulateDropdownsAsync()
    {
        var academicYears = await _academicYearService.GetAllAcademicYearsAsync();
        AcademicYears = academicYears.Select(ay => new SelectListItem
        {
            Value = ay.Id.ToString(),
            Text = ay.Name
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
