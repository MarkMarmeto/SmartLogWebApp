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

    [TempData]
    public string? StatusMessage { get; set; }

    [TempData]
    public string? ErrorMessage { get; set; }

    public async Task OnGetAsync()
    {
        GradeLevels = await _gradeSectionService.GetAllGradeLevelsAsync(activeOnly: false);
    }
}
