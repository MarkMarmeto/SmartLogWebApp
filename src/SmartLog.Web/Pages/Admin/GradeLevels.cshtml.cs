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

    public GradeLevelsModel(IGradeSectionService gradeSectionService)
    {
        _gradeSectionService = gradeSectionService;
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
}
