using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace SmartLog.Web.Pages.Admin;

[Authorize(Policy = "RequireAdmin")]
public class AboutModel : PageModel
{
    public string Version { get; } = "1.0.0";

    public void OnGet()
    {
    }
}
