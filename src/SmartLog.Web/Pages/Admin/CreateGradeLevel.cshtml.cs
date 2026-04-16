using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SmartLog.Web.Services;

namespace SmartLog.Web.Pages.Admin;

[Authorize(Policy = "RequireAdmin")]
public class CreateGradeLevelModel : PageModel
{
    private readonly IGradeSectionService _gradeSectionService;
    private readonly IAuditService _auditService;
    private readonly ILogger<CreateGradeLevelModel> _logger;

    public CreateGradeLevelModel(
        IGradeSectionService gradeSectionService,
        IAuditService auditService,
        ILogger<CreateGradeLevelModel> logger)
    {
        _gradeSectionService = gradeSectionService;
        _auditService = auditService;
        _logger = logger;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public class InputModel
    {
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
    }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        try
        {
            await _gradeSectionService.CreateGradeLevelAsync(Input.Code, Input.Name, Input.SortOrder);

            await _auditService.LogAsync(
                action: "CreateGradeLevel",
                details: $"Created grade level '{Input.Code}' - {Input.Name} by {User.Identity?.Name}");

            TempData["StatusMessage"] = $"Grade level '{Input.Name}' created successfully.";
            return RedirectToPage("/Admin/GradeLevels");
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return Page();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating grade level");
            ModelState.AddModelError(string.Empty, "An error occurred while creating the grade level.");
            return Page();
        }
    }
}
