using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace SmartLog.Web.Pages.Admin;

[Authorize(Policy = "CanManageSettings")]
public class SettingsModel : PageModel
{
    public void OnGet()
    {
    }
}
