using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SmartLog.Web.Data.Entities;
using SmartLog.Web.Services;
using Entities = SmartLog.Web.Data.Entities;

namespace SmartLog.Web.Pages.Admin;

[Authorize(Policy = "RequireAdmin")]
public class EditProgramModel : PageModel
{
    private readonly IGradeSectionService _gradeSectionService;
    private readonly IAuditService _auditService;
    private readonly ILogger<EditProgramModel> _logger;

    public EditProgramModel(IGradeSectionService gradeSectionService, IAuditService auditService, ILogger<EditProgramModel> logger)
    {
        _gradeSectionService = gradeSectionService;
        _auditService = auditService;
        _logger = logger;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public Guid ProgramId { get; set; }
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

        [Display(Name = "Is Active")]
        public bool IsActive { get; set; } = true;

        [Display(Name = "Grade Levels")]
        public List<Guid> GradeLevelIds { get; set; } = new();
    }

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        var program = await _gradeSectionService.GetProgramByIdAsync(id);
        if (program == null) return NotFound();

        ProgramId = id;

        Input = new InputModel
        {
            Code = program.Code,
            Name = program.Name,
            Description = program.Description,
            SortOrder = program.SortOrder,
            IsActive = program.IsActive,
            GradeLevelIds = program.GradeLevelPrograms.Select(g => g.GradeLevelId).ToList()
        };

        GradeLevels = await _gradeSectionService.GetAllGradeLevelsAsync(activeOnly: true);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(Guid id)
    {
        if (!ModelState.IsValid)
        {
            ProgramId = id;
            GradeLevels = await _gradeSectionService.GetAllGradeLevelsAsync(activeOnly: true);
            return Page();
        }

        try
        {
            var program = await _gradeSectionService.GetProgramByIdAsync(id);
            if (program == null) return NotFound();

            program.Code = Input.Code;
            program.Name = Input.Name;
            program.Description = Input.Description;
            program.SortOrder = Input.SortOrder;
            program.IsActive = Input.IsActive;

            await _gradeSectionService.UpdateProgramAsync(program, Input.GradeLevelIds);

            await _auditService.LogAsync("UpdateProgram", null, null, $"Updated program '{Input.Code}' by {User.Identity?.Name}");
            TempData["StatusMessage"] = $"Program '{Input.Code}' updated successfully.";
            return RedirectToPage("/Admin/Programs");
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            ProgramId = id;
            GradeLevels = await _gradeSectionService.GetAllGradeLevelsAsync(activeOnly: true);
            return Page();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating program {Id}", id);
            ModelState.AddModelError(string.Empty, "An unexpected error occurred.");
            ProgramId = id;
            GradeLevels = await _gradeSectionService.GetAllGradeLevelsAsync(activeOnly: true);
            return Page();
        }
    }
}
