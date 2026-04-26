using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SmartLog.Web.Data;
using SmartLog.Web.Data.Entities;
using SmartLog.Web.Services;
using Entities = SmartLog.Web.Data.Entities;

namespace SmartLog.Web.Pages.Admin;

[Authorize(Policy = "RequireAdmin")]
public class CreateSectionModel : PageModel
{
    private readonly IGradeSectionService _gradeSectionService;
    private readonly ApplicationDbContext _context;
    private readonly IAuditService _auditService;
    private readonly ILogger<CreateSectionModel> _logger;

    public CreateSectionModel(
        IGradeSectionService gradeSectionService,
        ApplicationDbContext context,
        IAuditService auditService,
        ILogger<CreateSectionModel> logger)
    {
        _gradeSectionService = gradeSectionService;
        _context = context;
        _auditService = auditService;
        _logger = logger;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public List<GradeLevel> GradeLevels { get; set; } = new();
    public List<Entities.Program> Programs { get; set; } = new();
    public List<Faculty> Faculty { get; set; } = new();

    public class InputModel
    {
        [Required]
        [Display(Name = "Grade Level")]
        public Guid GradeLevelId { get; set; }

        [Display(Name = "Program")]
        public Guid? ProgramId { get; set; }

        [Required]
        [StringLength(50)]
        public string Name { get; set; } = string.Empty;

        [Display(Name = "Adviser")]
        public Guid? AdviserId { get; set; }

        [Required]
        [Range(1, 100)]
        public int Capacity { get; set; } = 40;
    }

    public async Task OnGetAsync()
    {
        GradeLevels = await _gradeSectionService.GetAllGradeLevelsAsync(activeOnly: true);
        Faculty = await _context.Faculties.Where(f => f.IsActive).OrderBy(f => f.LastName).ToListAsync();
    }

    /// <summary>
    /// GET ?handler=ProgramsForGrade&gradeLevelId=xxx — AJAX endpoint for grade-level-filtered program list.
    /// </summary>
    public async Task<IActionResult> OnGetProgramsForGradeAsync(Guid gradeLevelId)
    {
        var programs = await _gradeSectionService.GetProgramsForGradeAsync(gradeLevelId);
        return new JsonResult(programs.Select(p => new { id = p.Id, code = p.Code, name = p.Name }));
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            GradeLevels = await _gradeSectionService.GetAllGradeLevelsAsync(activeOnly: true);
            if (Input.GradeLevelId != Guid.Empty)
                Programs = await _gradeSectionService.GetProgramsForGradeAsync(Input.GradeLevelId);
            Faculty = await _context.Faculties.Where(f => f.IsActive).OrderBy(f => f.LastName).ToListAsync();
            return Page();
        }

        try
        {
            await _gradeSectionService.CreateSectionAsync(
                Input.GradeLevelId,
                Input.Name,
                Input.ProgramId,
                Input.AdviserId,
                Input.Capacity);

            await _auditService.LogAsync(
                action: "CreateSection",
                details: $"Created section '{Input.Name}' by {User.Identity?.Name}");

            TempData["StatusMessage"] = $"Section '{Input.Name}' created successfully.";
            return RedirectToPage("/Admin/Sections");
        }
        catch (InvalidOperationException ex)
        {
            // US0103: validation errors (NG vs Program rule, missing FK lookups) bubble up here.
            var key = ex.Message.Contains("Program", StringComparison.OrdinalIgnoreCase)
                ? nameof(Input.ProgramId)
                : string.Empty;
            ModelState.AddModelError(key, ex.Message);
            GradeLevels = await _gradeSectionService.GetAllGradeLevelsAsync(activeOnly: true);
            if (Input.GradeLevelId != Guid.Empty)
                Programs = await _gradeSectionService.GetProgramsForGradeAsync(Input.GradeLevelId);
            Faculty = await _context.Faculties.Where(f => f.IsActive).OrderBy(f => f.LastName).ToListAsync();
            return Page();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating section");
            ModelState.AddModelError(string.Empty, "An error occurred while creating the section.");
            GradeLevels = await _gradeSectionService.GetAllGradeLevelsAsync(activeOnly: true);
            if (Input.GradeLevelId != Guid.Empty)
                Programs = await _gradeSectionService.GetProgramsForGradeAsync(Input.GradeLevelId);
            Faculty = await _context.Faculties.Where(f => f.IsActive).OrderBy(f => f.LastName).ToListAsync();
            return Page();
        }
    }
}
