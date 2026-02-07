using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SmartLog.Web.Data.Entities;
using SmartLog.Web.Services;

namespace SmartLog.Web.Pages.Account;

/// <summary>
/// Login page handler for SmartLog authentication.
/// Implements AC1-AC5 from US0001 and AC1-AC6 from US0002.
/// </summary>
[AllowAnonymous]
public class LoginModel : PageModel
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IAuditService _auditService;
    private readonly ILogger<LoginModel> _logger;

    public LoginModel(
        SignInManager<ApplicationUser> signInManager,
        UserManager<ApplicationUser> userManager,
        IAuditService auditService,
        ILogger<LoginModel> logger)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _auditService = auditService;
        _logger = logger;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public string? ReturnUrl { get; set; }

    [TempData]
    public string? ErrorMessage { get; set; }

    public class InputModel
    {
        [Required(ErrorMessage = "Username is required")]
        [Display(Name = "Username")]
        public string Username { get; set; } = string.Empty;

        [Required(ErrorMessage = "Password is required")]
        [DataType(DataType.Password)]
        [Display(Name = "Password")]
        public string Password { get; set; } = string.Empty;
    }

    public async Task<IActionResult> OnGetAsync(string? returnUrl = null)
    {
        // AC7 edge case: If already logged in, redirect to dashboard
        if (_signInManager.IsSignedIn(User))
        {
            _logger.LogInformation("User already authenticated, redirecting to dashboard");
            return RedirectToPage("/Index");
        }

        // Clear any existing external cookies
        await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);

        ReturnUrl = returnUrl ?? Url.Content("~/");

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
    {
        returnUrl ??= Url.Content("~/");

        if (!ModelState.IsValid)
        {
            return Page();
        }

        // Edge case 1: Trim leading/trailing spaces from username
        var username = Input.Username.Trim();

        // Edge case 3: Do NOT trim password - allow spaces
        var password = Input.Password;

        // Find user by username (case-insensitive by default in Identity)
        var user = await _userManager.FindByNameAsync(username);

        if (user == null)
        {
            // AC3: Invalid username - show generic error
            _logger.LogWarning("Login failed: User not found - {Username}", username);
            ErrorMessage = "Invalid username or password";
            return Page();
        }

        // AC4: Check if user account is active before attempting login
        if (!user.IsActive)
        {
            _logger.LogWarning("Login failed: User account deactivated - {Username}", username);
            ErrorMessage = "Your account has been deactivated. Please contact an administrator.";
            return Page();
        }

        // Attempt sign in
        var result = await _signInManager.PasswordSignInAsync(
            user,
            password,
            isPersistent: false,
            lockoutOnFailure: true);

        if (result.Succeeded)
        {
            // AC2: Successful login - redirect to dashboard
            _logger.LogInformation("User logged in: {Username}", username);
            return LocalRedirect(returnUrl);
        }

        if (result.RequiresTwoFactor)
        {
            // 2FA required - redirect to 2FA page (US0004)
            return RedirectToPage("/Account/LoginWith2fa", new { ReturnUrl = returnUrl });
        }

        if (result.IsLockedOut)
        {
            // US0002-AC2/AC3: Account locked out - show remaining time
            _logger.LogWarning("User account locked out: {Username}", username);

            var lockoutEnd = await _userManager.GetLockoutEndDateAsync(user);
            if (lockoutEnd.HasValue)
            {
                var remainingTime = lockoutEnd.Value - DateTimeOffset.UtcNow;
                var minutes = (int)Math.Ceiling(remainingTime.TotalMinutes);

                if (minutes > 0)
                {
                    // US0002-AC3: Show remaining lockout time
                    ErrorMessage = $"Your account is locked. Please try again in {minutes} minute{(minutes == 1 ? "" : "s")}.";
                }
                else
                {
                    ErrorMessage = "Your account is locked. Please try again shortly.";
                }
            }
            else
            {
                ErrorMessage = "Your account is locked. Please try again later.";
            }

            return Page();
        }

        // Check if this failed attempt triggered a lockout (5th attempt)
        var failedCount = await _userManager.GetAccessFailedCountAsync(user);
        var isNowLockedOut = await _userManager.IsLockedOutAsync(user);

        if (isNowLockedOut && failedCount == 0)
        {
            // US0002-AC2: Account just got locked (counter resets after lockout)
            // US0002-AC6: Log the lockout event
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
            var userAgent = Request.Headers.UserAgent.ToString();

            await _auditService.LogAsync(
                action: "AccountLocked",
                userId: user.Id,
                details: "Account locked after 5 failed attempts",
                ipAddress: ipAddress,
                userAgent: userAgent);

            _logger.LogWarning("Account locked after 5 failed attempts: {Username}", username);
            ErrorMessage = "Your account has been locked due to multiple failed login attempts. Please try again in 15 minutes.";
            return Page();
        }

        // AC3: Invalid password - show generic error
        _logger.LogWarning("Login failed: Invalid password - {Username}", username);
        ErrorMessage = "Invalid username or password";
        return Page();
    }
}
