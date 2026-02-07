using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SmartLog.Web.Data.Entities;
using SmartLog.Web.Services;

namespace SmartLog.Web.Pages.Account;

/// <summary>
/// Access denied page handler.
/// Implements US0007-AC8 (audit logging for unauthorized access).
/// </summary>
public class AccessDeniedModel : PageModel
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IAuditService _auditService;
    private readonly ILogger<AccessDeniedModel> _logger;

    public AccessDeniedModel(
        UserManager<ApplicationUser> userManager,
        IAuditService auditService,
        ILogger<AccessDeniedModel> logger)
    {
        _userManager = userManager;
        _auditService = auditService;
        _logger = logger;
    }

    public string? AttemptedPath { get; set; }

    public async Task OnGetAsync(string? returnUrl = null)
    {
        AttemptedPath = returnUrl ?? Request.Query["ReturnUrl"].ToString();

        var user = await _userManager.GetUserAsync(User);

        if (user != null)
        {
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
            var userAgent = Request.Headers.UserAgent.ToString();

            // US0007-AC8: Log unauthorized access attempt
            await _auditService.LogAsync(
                action: "UnauthorizedAccess",
                userId: user.Id,
                details: $"Attempted access to {AttemptedPath}",
                ipAddress: ipAddress,
                userAgent: userAgent);

            _logger.LogWarning(
                "Unauthorized access attempt by {Username} to {Path}",
                user.UserName, AttemptedPath);
        }
    }
}
