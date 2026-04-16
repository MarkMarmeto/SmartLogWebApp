using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SmartLog.Web.Data.Entities;
using SmartLog.Web.Services;
using Entities = SmartLog.Web.Data.Entities;

namespace SmartLog.Web.Pages.Admin;

[Authorize(Policy = "RequireAdmin")]
public class CreateProgramModel : PageModel
{
    private readonly IGradeSectionService _gradeSectionService;
    private readonly IAuditService _auditService;
    private readonly ILogger<CreateProgramModel> _logger;

    public CreateProgramModel(IGradeSectionService gradeSectionService, IAuditService auditService, ILogger<CreateProgramModel> logger)
    {
        _gradeSectionService = gradeSectionService;
        _auditService = auditService;
        _logger = logger;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public List<GradeLevel> GradeLevels { get; set; } = new();

    public class InputModel
    {
        [Required]
        [StringLength(20)]
        [RegularExpression(@"^[A-Za-z0-9\-_]+$", ErrorMessage = "Code may only contain letters, numbers, hyphens and underscores.")]
        [Display(Name = "Code")]
        public string Code { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        [Display(Name = "Name")]
        public string Name { get; set; } = string.Empty;

        [StringLength(500)]
        [Display(Name = "Description")]
        public string? Description { get; set; }

        [Display(Name = "Sort Order")]
        [Range(0, 9999)]
        public int SortOrder { get; set; }

        [Display(Name = "Grade Levels")]
        public List<Guid> GradeLevelIds { get; set; } = new();
    }

    public async Task OnGetAsync()
    {
        await LoadDropdownsAsync();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            await LoadDropdownsAsync();
            return Page();
        }

        try
        {
            var program = await _gradeSectionService.CreateProgramAsync(
                Input.Code,
                Input.Name,
                Input.Description,
                Input.SortOrder,
                Input.GradeLevelIds);

            await _auditService.LogAsync("CreateProgram", null, null, $"Created program '{program.Code}' by {User.Identity?.Name}");
            TempData["StatusMessage"] = $"Program '{program.Code}' created successfully.";
            return RedirectToPage("/Admin/Programs");
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            await LoadDropdownsAsync();
            return Page();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating program");
            ModelState.AddModelError(string.Empty, "An unexpected error occurred.");
            await LoadDropdownsAsync();
            return Page();
        }
    }

    private async Task LoadDropdownsAsync()
    {
        GradeLevels = await _gradeSectionService.GetAllGradeLevelsAsync(activeOnly: true);
    }
}
