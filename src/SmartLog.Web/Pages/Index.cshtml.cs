using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SmartLog.Web.Data.Entities;
using SmartLog.Web.Services;

namespace SmartLog.Web.Pages;

[Authorize]
public class IndexModel : PageModel
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IDashboardService _dashboardService;

    public IndexModel(UserManager<ApplicationUser> userManager, IDashboardService dashboardService)
    {
        _userManager = userManager;
        _dashboardService = dashboardService;
    }

    public string UserFullName { get; private set; } = string.Empty;
    public string UserRole { get; private set; } = string.Empty;
    public DashboardSummary Summary { get; private set; } = new();

    public async Task OnGetAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user != null)
        {
            UserFullName = user.FullName;
            var roles = await _userManager.GetRolesAsync(user);
            UserRole = roles.FirstOrDefault() ?? "User";
        }

        Summary = await _dashboardService.GetSummaryAsync();
    }
}
