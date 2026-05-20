using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SmartLog.Web.Data.Entities;
using SmartLog.Web.Services;

namespace SmartLog.Web.Pages.Admin;

/// <summary>
/// Single-pass print page (US0122 + US0123). Renders one visitor pass on a
/// CR100 portrait card with the school's branded header and orange accent band.
/// </summary>
[Authorize(Policy = "RequireAdmin")]
public class PrintVisitorPassModel : PageModel
{
    private readonly IVisitorPassService _visitorPassService;
    private readonly IAppSettingsService _appSettings;

    public PrintVisitorPassModel(
        IVisitorPassService visitorPassService,
        IAppSettingsService appSettings)
    {
        _visitorPassService = visitorPassService;
        _appSettings = appSettings;
    }

    public VisitorPass Pass { get; private set; } = null!;
    public string SchoolName { get; private set; } = "SmartLog School";
    public string? SchoolAddress { get; private set; }
    public string? SchoolLogoPath { get; private set; }

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        var pass = await _visitorPassService.GetByIdAsync(id);
        if (pass is null)
        {
            return NotFound();
        }

        Pass = pass;
        SchoolName = await _appSettings.GetAsync("System.SchoolName") ?? "SmartLog School";
        SchoolAddress = await _appSettings.GetAsync("Branding:SchoolAddress");
        SchoolLogoPath = await _appSettings.GetAsync("Branding:SchoolLogoPath");
        return Page();
    }
}
