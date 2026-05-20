using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SmartLog.Web.Data.Entities;
using SmartLog.Web.Services;

namespace SmartLog.Web.Pages.Admin;

/// <summary>
/// Single-pass print page (US0122). Renders one visitor pass on a CR100 portrait
/// card. Replaces the previous bulk-print page.
/// </summary>
[Authorize(Policy = "RequireAdmin")]
public class PrintVisitorPassModel : PageModel
{
    private readonly IVisitorPassService _visitorPassService;

    public PrintVisitorPassModel(IVisitorPassService visitorPassService)
    {
        _visitorPassService = visitorPassService;
    }

    public VisitorPass Pass { get; private set; } = null!;

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        var pass = await _visitorPassService.GetByIdAsync(id);
        if (pass is null)
        {
            return NotFound();
        }

        Pass = pass;
        return Page();
    }
}
