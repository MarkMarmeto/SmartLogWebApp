using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SmartLog.Web.Services;

namespace SmartLog.Web.Pages.Admin;

[Authorize(Policy = "RequireAdmin")]
public class EditGradeLevelModel : PageModel
{
    private readonly IGradeSectionService _gradeSectionService;
    private readonly IAuditService _auditService;
    private readonly ILogger<EditGradeLevelModel> _logger;

    public EditGradeLevelModel(
        IGradeSectionService gradeSectionService,
        IAuditService auditService,
        ILogger<EditGradeLevelModel> logger)
    {
        _gradeSectionService = gradeSectionService;
        _auditService = auditService;
        _logger = logger;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public class InputModel
    {
        public Guid Id { get; set; }

        [Required]
        [StringLength(10)]
        public string Code { get; set; } = string.Empty;

        [Required]
        [StringLength(50)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [Range(0, 100)]
        [Display(Name = "Sort Order")]
        public int SortOrder { get; set; }

        [Display(Name = "Is Active")]
        public bool IsActive { get; set; }
    }

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        var gradeLevel = await _gradeSectionService.GetGradeLevelByIdAsync(id);
        if (gradeLevel == null)
        {
            return NotFound();
        }

        Input = new InputModel
        {
            Id = gradeLevel.Id,
            Code = gradeLevel.Code,
            Name = gradeLevel.Name,
            SortOrder = gradeLevel.SortOrder,
            IsActive = gradeLevel.IsActive
        };

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        try
        {
            var gradeLevel = await _gradeSectionService.GetGradeLevelByIdAsync(Input.Id);
            if (gradeLevel == null)
            {
                return NotFound();
            }

            gradeLevel.Name = Input.Name;
            gradeLevel.SortOrder = Input.SortOrder;
            gradeLevel.IsActive = Input.IsActive;

            await _gradeSectionService.UpdateGradeLevelAsync(gradeLevel);

            await _auditService.LogAsync(
                action: "UpdateGradeLevel",
                userId: User.Identity?.Name,
                details: $"Updated grade level '{Input.Code}' - {Input.Name}");

            TempData["StatusMessage"] = $"Grade level '{Input.Name}' updated successfully.";
            return RedirectToPage("/Admin/GradeLevels");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating grade level");
            ModelState.AddModelError(string.Empty, "An error occurred while updating the grade level.");
            return Page();
        }
    }
}
