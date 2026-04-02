using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SmartLog.Web.Data;
using SmartLog.Web.Services;

namespace SmartLog.Web.Pages.Admin;

/// <summary>
/// Page for editing an existing academic year.
/// </summary>
[Authorize(Policy = "RequireAdmin")]
public class EditAcademicYearModel : PageModel
{
    private readonly ApplicationDbContext _context;
    private readonly IAcademicYearService _academicYearService;
    private readonly IAuditService _auditService;
    private readonly ILogger<EditAcademicYearModel> _logger;

    public EditAcademicYearModel(
        ApplicationDbContext context,
        IAcademicYearService academicYearService,
        IAuditService auditService,
        ILogger<EditAcademicYearModel> logger)
    {
        _context = context;
        _academicYearService = academicYearService;
        _auditService = auditService;
        _logger = logger;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public Guid AcademicYearId { get; set; }

    [TempData]
    public string? StatusMessage { get; set; }

    public class InputModel
    {
        public Guid Id { get; set; }

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
        public bool IsCurrent { get; set; }

        [Display(Name = "Active")]
        public bool IsActive { get; set; }
    }

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        var academicYear = await _context.AcademicYears.FindAsync(id);

        if (academicYear == null)
        {
            return NotFound();
        }

        AcademicYearId = id;

        Input = new InputModel
        {
            Id = academicYear.Id,
            Name = academicYear.Name,
            StartDate = academicYear.StartDate,
            EndDate = academicYear.EndDate,
            IsCurrent = academicYear.IsCurrent,
            IsActive = academicYear.IsActive
        };

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            AcademicYearId = Input.Id;
            return Page();
        }

        // Validate dates
        if (Input.EndDate <= Input.StartDate)
        {
            ModelState.AddModelError("Input.EndDate", "End date must be after start date.");
            AcademicYearId = Input.Id;
            return Page();
        }

        var academicYear = await _context.AcademicYears.FindAsync(Input.Id);

        if (academicYear == null)
        {
            return NotFound();
        }

        try
        {
            // Track what changed for audit log
            var changes = new List<string>();

            if (academicYear.Name != Input.Name)
                changes.Add($"Name: '{academicYear.Name}' → '{Input.Name}'");

            if (academicYear.StartDate != Input.StartDate)
                changes.Add($"Start Date: {academicYear.StartDate:yyyy-MM-dd} → {Input.StartDate:yyyy-MM-dd}");

            if (academicYear.EndDate != Input.EndDate)
                changes.Add($"End Date: {academicYear.EndDate:yyyy-MM-dd} → {Input.EndDate:yyyy-MM-dd}");

            // Update basic properties
            academicYear.Name = Input.Name;
            academicYear.StartDate = Input.StartDate;
            academicYear.EndDate = Input.EndDate;
            academicYear.IsActive = Input.IsActive;

            // Handle setting as current year
            if (Input.IsCurrent && !academicYear.IsCurrent)
            {
                // Unset any other current academic year
                var currentYear = await _context.AcademicYears
                    .FirstOrDefaultAsync(ay => ay.IsCurrent && ay.Id != Input.Id);

                if (currentYear != null)
                {
                    currentYear.IsCurrent = false;
                }

                academicYear.IsCurrent = true;
                changes.Add("Set as current academic year");
            }
            else if (!Input.IsCurrent && academicYear.IsCurrent)
            {
                academicYear.IsCurrent = false;
                changes.Add("Removed as current academic year");
            }

            if (academicYear.IsActive != Input.IsActive)
            {
                changes.Add($"Status: {(Input.IsActive ? "Activated" : "Deactivated")}");
            }

            await _context.SaveChangesAsync();

            var currentUser = User.Identity?.Name;
            await _auditService.LogAsync(
                action: "UpdateAcademicYear",
                userId: null,
                performedByUserId: null,
                details: $"Academic year '{Input.Name}' updated by {currentUser}: {string.Join(", ", changes)}",
                ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString(),
                userAgent: Request.Headers.UserAgent.ToString());

            _logger.LogInformation("Academic year {AcademicYearId} updated by {User}",
                Input.Id, currentUser);

            StatusMessage = $"Academic year '{Input.Name}' updated successfully.";
            return RedirectToPage("/Admin/AcademicYears");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating academic year {AcademicYearId}", Input.Id);
            ModelState.AddModelError(string.Empty, "An error occurred while updating the academic year.");
            AcademicYearId = Input.Id;
            return Page();
        }
    }
}
