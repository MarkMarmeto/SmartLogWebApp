using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SmartLog.Web.Data.Entities;
using SmartLog.Web.Services;

namespace SmartLog.Web.Pages.Account;

/// <summary>
/// Logout page handler.
/// Implements US0005-AC2, AC6.
/// </summary>
public class LogoutModel : PageModel
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IAuditService _auditService;
    private readonly ILogger<LogoutModel> _logger;

    public LogoutModel(
        SignInManager<ApplicationUser> signInManager,
        UserManager<ApplicationUser> userManager,
        IAuditService auditService,
        ILogger<LogoutModel> logger)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _auditService = auditService;
        _logger = logger;
    }

    public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
    {
        // Get user ID before signing out
        var user = await _userManager.GetUserAsync(User);
        var userId = user?.Id;
        var username = user?.UserName;

        // Sign out
        await _signInManager.SignOutAsync();
        _logger.LogInformation("User logged out: {Username}", username);

        // US0005-AC6: Log the logout event
        if (userId != null)
        {
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
            var userAgent = Request.Headers.UserAgent.ToString();

            await _auditService.LogAsync(
                action: "Logout",
                userId: userId,
                details: "User logged out",
                ipAddress: ipAddress,
                userAgent: userAgent);
        }

        // Display the logout confirmation page
        // Don't set TempData to avoid message persisting after re-login
        if (returnUrl != null)
        {
            return LocalRedirect(returnUrl);
        }

        return Page();
    }
}
