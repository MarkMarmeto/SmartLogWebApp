using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SmartLog.Web.Data.Entities;
using SmartLog.Web.Services;

namespace SmartLog.Web.Pages.Admin;

/// <summary>
/// Academic year management page.
/// Allows viewing, creating, and setting the current academic year.
/// </summary>
[Authorize(Policy = "RequireAdmin")]
public class AcademicYearsModel : PageModel
{
    private readonly IAcademicYearService _academicYearService;
    private readonly IAuditService _auditService;
    private readonly ILogger<AcademicYearsModel> _logger;

    public AcademicYearsModel(
        IAcademicYearService academicYearService,
        IAuditService auditService,
        ILogger<AcademicYearsModel> logger)
    {
        _academicYearService = academicYearService;
        _auditService = auditService;
        _logger = logger;
    }

    public List<AcademicYear> AcademicYears { get; set; } = new();

    [TempData]
    public string? StatusMessage { get; set; }

    [TempData]
    public string? ErrorMessage { get; set; }

    public async Task OnGetAsync()
    {
        AcademicYears = await _academicYearService.GetAllAcademicYearsAsync(activeOnly: false);
    }

    public async Task<IActionResult> OnPostSetCurrentAsync(int id)
    {
        try
        {
            await _academicYearService.SetCurrentAcademicYearAsync(id);

            var academicYear = await _academicYearService.GetAcademicYearByIdAsync(id);

            await _auditService.LogAsync(
                action: "SetCurrentAcademicYear",
                userId: User.Identity?.Name,
                details: $"Set academic year '{academicYear?.Name}' as current");

            StatusMessage = $"Academic year '{academicYear?.Name}' is now set as current.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting current academic year");
            ErrorMessage = $"Error: {ex.Message}";
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeactivateAsync(int id)
    {
        try
        {
            var academicYear = await _academicYearService.GetAcademicYearByIdAsync(id);
            if (academicYear == null)
            {
                ErrorMessage = "Academic year not found.";
                return RedirectToPage();
            }

            await _academicYearService.DeactivateAcademicYearAsync(id);

            await _auditService.LogAsync(
                action: "DeactivateAcademicYear",
                userId: User.Identity?.Name,
                details: $"Deactivated academic year '{academicYear.Name}'");

            StatusMessage = $"Academic year '{academicYear.Name}' has been deactivated.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deactivating academic year");
            ErrorMessage = $"Error: {ex.Message}";
        }

        return RedirectToPage();
    }
}
