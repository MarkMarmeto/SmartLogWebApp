using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SmartLog.Web.Data.Entities;

namespace SmartLog.Web.Pages;

/// <summary>
/// Dashboard page - requires authentication.
/// Shows welcome message with user's name (AC2).
/// </summary>
[Authorize]
public class IndexModel : PageModel
{
    private readonly UserManager<ApplicationUser> _userManager;

    public IndexModel(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    public string UserFullName { get; private set; } = string.Empty;
    public string UserRole { get; private set; } = string.Empty;

    public async Task OnGetAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user != null)
        {
            UserFullName = user.FullName;
            var roles = await _userManager.GetRolesAsync(user);
            UserRole = roles.FirstOrDefault() ?? "User";
        }
    }
}
