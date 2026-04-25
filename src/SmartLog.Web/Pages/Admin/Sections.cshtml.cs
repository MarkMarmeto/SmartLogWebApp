using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SmartLog.Web.Data.Entities;
using SmartLog.Web.Services;

namespace SmartLog.Web.Pages.Admin;

[Authorize(Policy = "RequireAdmin")]
public class SectionsModel : PageModel
{
    private readonly IGradeSectionService _gradeSectionService;
    private readonly IAuditService _auditService;
    private readonly ILogger<SectionsModel> _logger;

    public SectionsModel(IGradeSectionService gradeSectionService, IAuditService auditService, ILogger<SectionsModel> logger)
    {
        _gradeSectionService = gradeSectionService;
        _auditService = auditService;
        _logger = logger;
    }

    public List<Section> Sections { get; set; } = new();
    public List<GradeLevel> GradeLevels { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public Guid? GradeId { get; set; }

    [BindProperty(SupportsGet = true)]
    public int PageNumber { get; set; } = 1;

    public int PageSize { get; set; } = 20;
    public int TotalPages { get; set; }
    public int TotalSections { get; set; }

    [TempData]
    public string? StatusMessage { get; set; }

    [TempData]
    public string? ErrorMessage { get; set; }

    public async Task OnGetAsync()
    {
        GradeLevels = await _gradeSectionService.GetAllGradeLevelsAsync(activeOnly: true);

        List<Section> allSections;
        if (GradeId.HasValue)
            allSections = await _gradeSectionService.GetSectionsByGradeAsync(GradeId.Value, activeOnly: false);
        else
            allSections = await _gradeSectionService.GetAllSectionsAsync(activeOnly: false);

        TotalSections = allSections.Count;
        TotalPages = (int)Math.Ceiling(TotalSections / (double)PageSize);

        if (PageNumber < 1) PageNumber = 1;
        if (PageNumber > TotalPages && TotalPages > 0) PageNumber = TotalPages;

        Sections = allSections
            .OrderBy(s => s.GradeLevel.SortOrder)
            .ThenBy(s => s.Program?.SortOrder ?? 0)
            .ThenBy(s => s.Name)
            .Skip((PageNumber - 1) * PageSize)
            .Take(PageSize)
            .ToList();
    }

    public async Task<IActionResult> OnPostDeleteAsync(Guid id)
    {
        if (!User.IsInRole("SuperAdmin")) return Forbid();
        try
        {
            await _gradeSectionService.DeleteSectionAsync(id);
            await _auditService.LogAsync("DeleteSection", null, null, $"Deleted section ID {id} by {User.Identity?.Name}");
            StatusMessage = "Section deleted successfully.";
        }
        catch (InvalidOperationException ex)
        {
            ErrorMessage = ex.Message;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting section {Id}", id);
            ErrorMessage = "An unexpected error occurred while deleting the section.";
        }
        return RedirectToPage();
    }
}
