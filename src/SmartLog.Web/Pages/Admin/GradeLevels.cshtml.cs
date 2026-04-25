using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SmartLog.Web.Data.Entities;
using SmartLog.Web.Services;

namespace SmartLog.Web.Pages.Admin;

[Authorize(Policy = "RequireAdmin")]
public class GradeLevelsModel : PageModel
{
    private readonly IGradeSectionService _gradeSectionService;
    private readonly IAuditService _auditService;
    private readonly ILogger<GradeLevelsModel> _logger;

    public GradeLevelsModel(IGradeSectionService gradeSectionService, IAuditService auditService, ILogger<GradeLevelsModel> logger)
    {
        _gradeSectionService = gradeSectionService;
        _auditService = auditService;
        _logger = logger;
    }

    public List<GradeLevel> GradeLevels { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public int PageNumber { get; set; } = 1;

    public int PageSize { get; set; } = 20;
    public int TotalPages { get; set; }
    public int TotalGradeLevels { get; set; }

    [TempData]
    public string? StatusMessage { get; set; }

    [TempData]
    public string? ErrorMessage { get; set; }

    public async Task OnGetAsync()
    {
        var allGrades = await _gradeSectionService.GetAllGradeLevelsAsync(activeOnly: false);

        TotalGradeLevels = allGrades.Count;
        TotalPages = (int)Math.Ceiling(TotalGradeLevels / (double)PageSize);

        if (PageNumber < 1) PageNumber = 1;
        if (PageNumber > TotalPages && TotalPages > 0) PageNumber = TotalPages;

        GradeLevels = allGrades
            .OrderBy(g => g.SortOrder)
            .ThenBy(g => g.Name)
            .Skip((PageNumber - 1) * PageSize)
            .Take(PageSize)
            .ToList();
    }

    public async Task<IActionResult> OnPostDeleteAsync(Guid id)
    {
        if (!User.IsInRole("SuperAdmin")) return Forbid();
        try
        {
            await _gradeSectionService.DeleteGradeLevelAsync(id);
            await _auditService.LogAsync("DeleteGradeLevel", null, null, $"Deleted grade level ID {id} by {User.Identity?.Name}");
            StatusMessage = "Grade level deleted successfully.";
        }
        catch (InvalidOperationException ex)
        {
            ErrorMessage = ex.Message;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting grade level {Id}", id);
            ErrorMessage = "An unexpected error occurred while deleting the grade level.";
        }
        return RedirectToPage();
    }
}
