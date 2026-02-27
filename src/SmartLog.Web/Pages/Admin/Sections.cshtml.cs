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

    public SectionsModel(IGradeSectionService gradeSectionService)
    {
        _gradeSectionService = gradeSectionService;
    }

    public List<Section> Sections { get; set; } = new();
    public List<GradeLevel> GradeLevels { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public int? GradeId { get; set; }

    [BindProperty(SupportsGet = true)]
    public int PageNumber { get; set; } = 1;

    public int PageSize { get; set; } = 20;
    public int TotalPages { get; set; }
    public int TotalSections { get; set; }

    [TempData]
    public string? StatusMessage { get; set; }

    public async Task OnGetAsync()
    {
        GradeLevels = await _gradeSectionService.GetAllGradeLevelsAsync(activeOnly: true);

        List<Section> allSections;
        if (GradeId.HasValue)
        {
            allSections = await _gradeSectionService.GetSectionsByGradeAsync(GradeId.Value, activeOnly: false);
        }
        else
        {
            allSections = await _gradeSectionService.GetAllSectionsAsync(activeOnly: false);
        }

        TotalSections = allSections.Count;
        TotalPages = (int)Math.Ceiling(TotalSections / (double)PageSize);

        if (PageNumber < 1) PageNumber = 1;
        if (PageNumber > TotalPages && TotalPages > 0) PageNumber = TotalPages;

        Sections = allSections
            .OrderBy(s => s.GradeLevel.Name)
            .ThenBy(s => s.Name)
            .Skip((PageNumber - 1) * PageSize)
            .Take(PageSize)
            .ToList();
    }
}
