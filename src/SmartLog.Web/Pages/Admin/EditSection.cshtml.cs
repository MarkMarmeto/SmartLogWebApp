using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SmartLog.Web.Data;
using SmartLog.Web.Data.Entities;
using SmartLog.Web.Services;

namespace SmartLog.Web.Pages.Admin;

[Authorize(Policy = "RequireAdmin")]
public class EditSectionModel : PageModel
{
    private readonly IGradeSectionService _gradeSectionService;
    private readonly ApplicationDbContext _context;
    private readonly IAuditService _auditService;
    private readonly ILogger<EditSectionModel> _logger;

    public EditSectionModel(
        IGradeSectionService gradeSectionService,
        ApplicationDbContext context,
        IAuditService auditService,
        ILogger<EditSectionModel> logger)
    {
        _gradeSectionService = gradeSectionService;
        _context = context;
        _auditService = auditService;
        _logger = logger;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public List<Faculty> Faculty { get; set; } = new();
    public string GradeLevelName { get; set; } = string.Empty;

    public class InputModel
    {
        public int Id { get; set; }

        [Required]
        [StringLength(50)]
        public string Name { get; set; } = string.Empty;

        [Display(Name = "Adviser")]
        public int? AdviserId { get; set; }

        [Required]
        [Range(1, 100)]
        public int Capacity { get; set; }

        [Display(Name = "Is Active")]
        public bool IsActive { get; set; }
    }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var section = await _gradeSectionService.GetSectionByIdAsync(id);
        if (section == null)
        {
            return NotFound();
        }

        Input = new InputModel
        {
            Id = section.Id,
            Name = section.Name,
            AdviserId = section.AdviserId,
            Capacity = section.Capacity,
            IsActive = section.IsActive
        };

        GradeLevelName = section.GradeLevel.Name;
        Faculty = await _context.Faculties.Where(f => f.IsActive).OrderBy(f => f.LastName).ToListAsync();

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            Faculty = await _context.Faculties.Where(f => f.IsActive).OrderBy(f => f.LastName).ToListAsync();
            return Page();
        }

        try
        {
            var section = await _gradeSectionService.GetSectionByIdAsync(Input.Id);
            if (section == null)
            {
                return NotFound();
            }

            section.Name = Input.Name;
            section.AdviserId = Input.AdviserId;
            section.Capacity = Input.Capacity;
            section.IsActive = Input.IsActive;

            await _gradeSectionService.UpdateSectionAsync(section);

            await _auditService.LogAsync(
                action: "UpdateSection",
                userId: User.Identity?.Name,
                details: $"Updated section '{Input.Name}'");

            TempData["StatusMessage"] = $"Section '{Input.Name}' updated successfully.";
            return RedirectToPage("/Admin/Sections");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating section");
            ModelState.AddModelError(string.Empty, "An error occurred while updating the section.");
            Faculty = await _context.Faculties.Where(f => f.IsActive).OrderBy(f => f.LastName).ToListAsync();
            return Page();
        }
    }
}
