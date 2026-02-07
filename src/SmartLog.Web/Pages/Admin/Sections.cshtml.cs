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

    [TempData]
    public string? StatusMessage { get; set; }

    public async Task OnGetAsync()
    {
        GradeLevels = await _gradeSectionService.GetAllGradeLevelsAsync(activeOnly: true);

        if (GradeId.HasValue)
        {
            Sections = await _gradeSectionService.GetSectionsByGradeAsync(GradeId.Value, activeOnly: false);
        }
        else
        {
            Sections = await _gradeSectionService.GetAllSectionsAsync(activeOnly: false);
        }
    }
}
