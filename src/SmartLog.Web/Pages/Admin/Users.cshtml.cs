using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SmartLog.Web.Data.Entities;
using SmartLog.Web.Services;

namespace SmartLog.Web.Pages.Admin;

/// <summary>
/// User management page.
/// Implements US0002-AC7 (Manual Unlock), US0011 (Deactivate/Reactivate),
/// US0012 (Reset Password), US0013 (Search/Filter).
/// </summary>
[Authorize(Policy = "CanManageUsers")]
public class UsersModel : PageModel
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly IAuditService _auditService;
    private readonly ILogger<UsersModel> _logger;

    public UsersModel(
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        IAuditService auditService,
        ILogger<UsersModel> logger)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _auditService = auditService;
        _logger = logger;
    }

    public List<UserViewModel> Users { get; set; } = new();
    public List<string> AvailableRoles { get; set; } = new();

    // US0013: Search and filter parameters
    [BindProperty(SupportsGet = true)]
    public string? SearchTerm { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? RoleFilter { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? StatusFilter { get; set; }

    [BindProperty(SupportsGet = true)]
    public int PageNumber { get; set; } = 1;

    public int PageSize { get; set; } = 20;
    public int TotalPages { get; set; }
    public int TotalUsers { get; set; }

    [TempData]
    public string? StatusMessage { get; set; }

    [TempData]
    public string? ErrorMessage { get; set; }

    public async Task OnGetAsync()
    {
        var currentUser = await _userManager.GetUserAsync(User);
        var isSuperAdmin = currentUser != null && await _userManager.IsInRoleAsync(currentUser, "SuperAdmin");

        // Load available roles for filter
        AvailableRoles = _roleManager.Roles.Select(r => r.Name).Where(n => n != null).Cast<string>().ToList();

        // Start with all users
        var query = _userManager.Users.AsQueryable();

        // US0013-AC2, AC3: Search by name or username
        if (!string.IsNullOrWhiteSpace(SearchTerm))
        {
            var searchLower = SearchTerm.ToLower();
            query = query.Where(u =>
                u.UserName!.ToLower().Contains(searchLower) ||
                u.FirstName.ToLower().Contains(searchLower) ||
                u.LastName.ToLower().Contains(searchLower) ||
                u.Email!.ToLower().Contains(searchLower));
        }

        // US0013-AC5: Filter by status
        if (!string.IsNullOrWhiteSpace(StatusFilter))
        {
            switch (StatusFilter.ToLower())
            {
                case "active":
                    query = query.Where(u => u.IsActive && (u.LockoutEnd == null || u.LockoutEnd <= DateTimeOffset.UtcNow));
                    break;
                case "inactive":
                    query = query.Where(u => !u.IsActive);
                    break;
                case "locked":
                    query = query.Where(u => u.LockoutEnd != null && u.LockoutEnd > DateTimeOffset.UtcNow);
                    break;
            }
        }

        // Get total count for pagination
        TotalUsers = query.Count();
        TotalPages = (int)Math.Ceiling(TotalUsers / (double)PageSize);

        // Ensure page number is valid
        if (PageNumber < 1) PageNumber = 1;
        if (PageNumber > TotalPages && TotalPages > 0) PageNumber = TotalPages;

        // US0013-AC1: Pagination (20 per page)
        var users = query
            .OrderBy(u => u.UserName)
            .Skip((PageNumber - 1) * PageSize)
            .Take(PageSize)
            .ToList();

        Users = new List<UserViewModel>();

        foreach (var user in users)
        {
            var roles = await _userManager.GetRolesAsync(user);
            var isLockedOut = await _userManager.IsLockedOutAsync(user);
            var lockoutEnd = await _userManager.GetLockoutEndDateAsync(user);
            var isSuperAdminUser = roles.Contains("SuperAdmin");
            var isCurrentUser = currentUser?.Id == user.Id;

            // US0013-AC4: Filter by role (done after loading roles)
            if (!string.IsNullOrWhiteSpace(RoleFilter) && !roles.Contains(RoleFilter))
            {
                TotalUsers--; // Adjust count
                continue;
            }

            // US0010-AC4, US0011-AC5: Admin can only manage non-SuperAdmin users
            var canEdit = !isCurrentUser && (isSuperAdmin || !isSuperAdminUser);
            var canDeactivate = !isCurrentUser && (isSuperAdmin || !isSuperAdminUser);

            Users.Add(new UserViewModel
            {
                Id = user.Id,
                UserName = user.UserName ?? string.Empty,
                Email = user.Email ?? string.Empty,
                FullName = user.FullName,
                IsActive = user.IsActive,
                IsLockedOut = isLockedOut,
                LockoutEnd = lockoutEnd?.UtcDateTime,
                Roles = roles.ToList(),
                IsCurrentUser = isCurrentUser,
                MustChangePassword = user.MustChangePassword,
                CanEdit = canEdit,
                CanDeactivate = canDeactivate
            });
        }

        // Recalculate pagination after role filter
        if (!string.IsNullOrWhiteSpace(RoleFilter))
        {
            TotalPages = (int)Math.Ceiling(TotalUsers / (double)PageSize);
        }
    }

    /// <summary>
    /// US0002-AC7: Manual unlock by Super Admin.
    /// </summary>
    public async Task<IActionResult> OnPostUnlockAccountAsync(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
        {
            ErrorMessage = "User not found.";
            return RedirectToPage();
        }

        // Reset lockout end date
        var result = await _userManager.SetLockoutEndDateAsync(user, null);
        if (!result.Succeeded)
        {
            ErrorMessage = "Failed to unlock account.";
            _logger.LogError("Failed to unlock account for user {UserId}: {Errors}",
                userId, string.Join(", ", result.Errors.Select(e => e.Description)));
            return RedirectToPage();
        }

        // Reset failed access count
        await _userManager.ResetAccessFailedCountAsync(user);

        // Get current user who performed the unlock
        var currentUser = await _userManager.GetUserAsync(User);
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
        var userAgent = Request.Headers.UserAgent.ToString();

        // US0002-AC7: Log the unlock event
        await _auditService.LogAsync(
            action: "AccountUnlocked",
            userId: user.Id,
            performedByUserId: currentUser?.Id,
            details: $"Account manually unlocked by Super Admin {currentUser?.UserName}",
            ipAddress: ipAddress,
            userAgent: userAgent);

        _logger.LogInformation("Account unlocked for user {Username} by {AdminUsername}",
            user.UserName, currentUser?.UserName);

        StatusMessage = "Account unlocked successfully";
        return RedirectToPage();
    }

    /// <summary>
    /// US0011-AC1: Deactivate user (soft delete).
    /// </summary>
    public async Task<IActionResult> OnPostDeactivateUserAsync(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
        {
            ErrorMessage = "User not found.";
            return RedirectToPage();
        }

        var currentUser = await _userManager.GetUserAsync(User);

        // US0011-AC4: Cannot deactivate self
        if (currentUser?.Id == user.Id)
        {
            ErrorMessage = "You cannot deactivate your own account.";
            return RedirectToPage();
        }

        // Check if this is the last Super Admin
        var isSuperAdmin = await _userManager.IsInRoleAsync(user, "SuperAdmin");
        if (isSuperAdmin)
        {
            var allSuperAdmins = await _userManager.GetUsersInRoleAsync("SuperAdmin");
            var activeSuperAdmins = allSuperAdmins.Count(u => u.IsActive);
            if (activeSuperAdmins <= 1)
            {
                ErrorMessage = "Cannot deactivate the last Super Admin.";
                return RedirectToPage();
            }
        }

        // Deactivate user
        user.IsActive = false;
        user.UpdatedAt = DateTime.UtcNow;
        var result = await _userManager.UpdateAsync(user);

        if (!result.Succeeded)
        {
            ErrorMessage = "Failed to deactivate user.";
            _logger.LogError("Failed to deactivate user {UserId}: {Errors}",
                userId, string.Join(", ", result.Errors.Select(e => e.Description)));
            return RedirectToPage();
        }

        // Audit log
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
        var userAgent = Request.Headers.UserAgent.ToString();

        await _auditService.LogAsync(
            action: "UserDeactivated",
            userId: user.Id,
            performedByUserId: currentUser?.Id,
            details: $"User '{user.UserName}' deactivated by {currentUser?.UserName}",
            ipAddress: ipAddress,
            userAgent: userAgent);

        _logger.LogInformation("User {Username} deactivated by {AdminUsername}",
            user.UserName, currentUser?.UserName);

        StatusMessage = "User deactivated successfully";
        return RedirectToPage();
    }

    /// <summary>
    /// US0011-AC3: Reactivate user.
    /// </summary>
    public async Task<IActionResult> OnPostReactivateUserAsync(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
        {
            ErrorMessage = "User not found.";
            return RedirectToPage();
        }

        // Reactivate user
        user.IsActive = true;
        user.UpdatedAt = DateTime.UtcNow;
        var result = await _userManager.UpdateAsync(user);

        if (!result.Succeeded)
        {
            ErrorMessage = "Failed to reactivate user.";
            _logger.LogError("Failed to reactivate user {UserId}: {Errors}",
                userId, string.Join(", ", result.Errors.Select(e => e.Description)));
            return RedirectToPage();
        }

        // Audit log
        var currentUser = await _userManager.GetUserAsync(User);
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
        var userAgent = Request.Headers.UserAgent.ToString();

        await _auditService.LogAsync(
            action: "UserReactivated",
            userId: user.Id,
            performedByUserId: currentUser?.Id,
            details: $"User '{user.UserName}' reactivated by {currentUser?.UserName}",
            ipAddress: ipAddress,
            userAgent: userAgent);

        _logger.LogInformation("User {Username} reactivated by {AdminUsername}",
            user.UserName, currentUser?.UserName);

        StatusMessage = "User reactivated successfully";
        return RedirectToPage();
    }

    /// <summary>
    /// US0012: Reset user password.
    /// </summary>
    public async Task<IActionResult> OnPostResetPasswordAsync(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
        {
            return new JsonResult(new { success = false, error = "User not found" });
        }

        var currentUser = await _userManager.GetUserAsync(User);

        // US0012-AC4: Cannot reset own password via this function
        if (currentUser?.Id == user.Id)
        {
            return new JsonResult(new { success = false, error = "Cannot reset your own password. Use your profile page." });
        }

        // US0012-AC5: Check permissions for Super Admin
        var isSuperAdmin = currentUser != null && await _userManager.IsInRoleAsync(currentUser, "SuperAdmin");
        var targetIsSuperAdmin = await _userManager.IsInRoleAsync(user, "SuperAdmin");

        if (!isSuperAdmin && targetIsSuperAdmin)
        {
            return new JsonResult(new { success = false, error = "You do not have permission to reset this user's password" });
        }

        // Generate new temporary password
        var tempPassword = GenerateTemporaryPassword();

        // Remove old password and set new one
        await _userManager.RemovePasswordAsync(user);
        var result = await _userManager.AddPasswordAsync(user, tempPassword);

        if (!result.Succeeded)
        {
            _logger.LogError("Failed to reset password for user {UserId}: {Errors}",
                userId, string.Join(", ", result.Errors.Select(e => e.Description)));
            return new JsonResult(new { success = false, error = "Failed to reset password" });
        }

        // Set force password change flag
        user.MustChangePassword = true;
        user.UpdatedAt = DateTime.UtcNow;
        await _userManager.UpdateAsync(user);

        // If user was locked, unlock them
        if (await _userManager.IsLockedOutAsync(user))
        {
            await _userManager.SetLockoutEndDateAsync(user, null);
            await _userManager.ResetAccessFailedCountAsync(user);
        }

        // Audit log
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
        var userAgent = Request.Headers.UserAgent.ToString();

        await _auditService.LogAsync(
            action: "PasswordReset",
            userId: user.Id,
            performedByUserId: currentUser?.Id,
            details: $"Password reset by {currentUser?.UserName}",
            ipAddress: ipAddress,
            userAgent: userAgent);

        _logger.LogInformation("Password reset for user {Username} by {AdminUsername}",
            user.UserName, currentUser?.UserName);

        return new JsonResult(new { success = true, password = tempPassword });
    }

    private static string GenerateTemporaryPassword()
    {
        return PasswordGenerator.GenerateTemporaryPassword();
    }

    public class UserViewModel
    {
        public string Id { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public bool IsLockedOut { get; set; }
        public DateTime? LockoutEnd { get; set; }
        public List<string> Roles { get; set; } = new();
        public bool IsCurrentUser { get; set; }
        public bool MustChangePassword { get; set; }
        public bool CanEdit { get; set; }
        public bool CanDeactivate { get; set; }
    }
}
