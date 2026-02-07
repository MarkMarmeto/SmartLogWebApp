using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SmartLog.Web.Services;

namespace SmartLog.Web.Pages.Admin;

/// <summary>
/// Page for creating a new academic year.
/// </summary>
[Authorize(Policy = "RequireAdmin")]
public class CreateAcademicYearModel : PageModel
{
    private readonly IAcademicYearService _academicYearService;
    private readonly IAuditService _auditService;
    private readonly ILogger<CreateAcademicYearModel> _logger;

    public CreateAcademicYearModel(
        IAcademicYearService academicYearService,
        IAuditService auditService,
        ILogger<CreateAcademicYearModel> logger)
    {
        _academicYearService = academicYearService;
        _auditService = auditService;
        _logger = logger;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public class InputModel
    {
        [Required]
        [StringLength(20, ErrorMessage = "Name must be 20 characters or less")]
        [Display(Name = "Academic Year Name")]
        public string Name { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Start Date")]
        public DateTime StartDate { get; set; }

        [Required]
        [Display(Name = "End Date")]
        public DateTime EndDate { get; set; }

        [Display(Name = "Set as Current")]
        public bool SetAsCurrent { get; set; }
    }

    public void OnGet()
    {
        // Pre-fill with suggested dates for next academic year
        var now = DateTime.UtcNow;
        var nextYear = now.Month >= 6 ? now.Year + 1 : now.Year;

        Input.Name = $"{nextYear}-{nextYear + 1}";
        Input.StartDate = new DateTime(nextYear, 8, 1); // August 1
        Input.EndDate = new DateTime(nextYear + 1, 5, 31); // May 31
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        // Validate dates
        if (Input.EndDate <= Input.StartDate)
        {
            ModelState.AddModelError("Input.EndDate", "End date must be after start date.");
            return Page();
        }

        try
        {
            var academicYear = await _academicYearService.CreateAcademicYearAsync(
                Input.Name,
                Input.StartDate,
                Input.EndDate,
                Input.SetAsCurrent);

            await _auditService.LogAsync(
                action: "CreateAcademicYear",
                userId: User.Identity?.Name,
                details: $"Created academic year '{Input.Name}'{(Input.SetAsCurrent ? " and set as current" : "")}");

            TempData["StatusMessage"] = $"Academic year '{Input.Name}' created successfully.";

            return RedirectToPage("/Admin/AcademicYears");
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return Page();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating academic year");
            ModelState.AddModelError(string.Empty, "An error occurred while creating the academic year.");
            return Page();
        }
    }
}
