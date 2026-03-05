using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SmartLog.Web.Data.Entities;
using SmartLog.Web.Services;

namespace SmartLog.Web.Pages.Account;

[Authorize]
public class ChangePasswordModel : PageModel
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly IAuditService _auditService;
    private readonly ILogger<ChangePasswordModel> _logger;

    public ChangePasswordModel(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        IAuditService auditService,
        ILogger<ChangePasswordModel> logger)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _auditService = auditService;
        _logger = logger;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    [TempData]
    public string? StatusMessage { get; set; }

    [TempData]
    public string? ErrorMessage { get; set; }

    public bool IsForcedChange { get; set; }

    public class InputModel
    {
        [Required(ErrorMessage = "Current password is required")]
        [DataType(DataType.Password)]
        [Display(Name = "Current Password")]
        public string CurrentPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "New password is required")]
        [StringLength(100, MinimumLength = 8, ErrorMessage = "Password must be at least 8 characters")]
        [DataType(DataType.Password)]
        [Display(Name = "New Password")]
        public string NewPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please confirm your new password")]
        [DataType(DataType.Password)]
        [Display(Name = "Confirm New Password")]
        [Compare("NewPassword", ErrorMessage = "The new password and confirmation do not match")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }

    public async Task<IActionResult> OnGetAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return RedirectToPage("/Account/Login");
        }

        IsForcedChange = user.MustChangePassword;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return RedirectToPage("/Account/Login");
        }

        IsForcedChange = user.MustChangePassword;

        if (!ModelState.IsValid)
        {
            return Page();
        }

        // Verify current password
        var isCurrentPasswordValid = await _userManager.CheckPasswordAsync(user, Input.CurrentPassword);
        if (!isCurrentPasswordValid)
        {
            ErrorMessage = "Current password is incorrect.";
            return Page();
        }

        // Ensure new password is different from current
        if (Input.CurrentPassword == Input.NewPassword)
        {
            ErrorMessage = "New password must be different from your current password.";
            return Page();
        }

        // Change password
        var result = await _userManager.ChangePasswordAsync(user, Input.CurrentPassword, Input.NewPassword);
        if (!result.Succeeded)
        {
            ErrorMessage = string.Join(" ", result.Errors.Select(e => e.Description));
            return Page();
        }

        // Clear force password change flag
        if (user.MustChangePassword)
        {
            user.MustChangePassword = false;
            user.UpdatedAt = DateTime.UtcNow;
            await _userManager.UpdateAsync(user);
        }

        // Refresh sign-in to update security stamp
        await _signInManager.RefreshSignInAsync(user);

        // Audit log
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
        var userAgent = Request.Headers.UserAgent.ToString();

        await _auditService.LogAsync(
            action: "PasswordChanged",
            userId: user.Id,
            performedByUserId: user.Id,
            details: "Password changed by user",
            ipAddress: ipAddress,
            userAgent: userAgent);

        _logger.LogInformation("User {Username} changed their password", user.UserName);

        StatusMessage = "Your password has been changed successfully.";
        return RedirectToPage("/Account/Profile");
    }
}
