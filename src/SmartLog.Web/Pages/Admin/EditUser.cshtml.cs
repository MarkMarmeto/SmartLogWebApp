using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SmartLog.Web.Data.Entities;
using SmartLog.Web.Services;

namespace SmartLog.Web.Pages.Admin;

/// <summary>
/// Edit user page.
/// Implements US0010 (Edit User Details).
/// </summary>
[Authorize(Policy = "CanManageUsers")]
public class EditUserModel : PageModel
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly IAuditService _auditService;
    private readonly ILogger<EditUserModel> _logger;

    public EditUserModel(
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        IAuditService auditService,
        ILogger<EditUserModel> logger)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _auditService = auditService;
        _logger = logger;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public List<string> AvailableRoles { get; set; } = new();

    public bool IsEditingSelf { get; set; }

    [TempData]
    public string? ErrorMessage { get; set; }

    private string? UserId { get; set; }

    public class InputModel
    {
        public string Username { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Please enter a valid email address")]
        [StringLength(256)]
        [Display(Name = "Email")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "First name is required")]
        [StringLength(100)]
        [Display(Name = "First Name")]
        public string FirstName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Last name is required")]
        [StringLength(100)]
        [Display(Name = "Last Name")]
        public string LastName { get; set; } = string.Empty;

        [Phone(ErrorMessage = "Please enter a valid phone number")]
        [StringLength(20)]
        [Display(Name = "Phone Number")]
        public string? PhoneNumber { get; set; }

        [Required(ErrorMessage = "Role is required")]
        [Display(Name = "Role")]
        public string Role { get; set; } = string.Empty;
    }

    public async Task<IActionResult> OnGetAsync(string id)
    {
        UserId = id;

        var user = await _userManager.FindByIdAsync(id);
        if (user == null)
        {
            return NotFound();
        }

        var currentUser = await _userManager.GetUserAsync(User);
        IsEditingSelf = currentUser?.Id == user.Id;

        var roles = await _userManager.GetRolesAsync(user);
        var currentRole = roles.FirstOrDefault() ?? string.Empty;

        Input = new InputModel
        {
            Username = user.UserName ?? string.Empty,
            Email = user.Email ?? string.Empty,
            FirstName = user.FirstName,
            LastName = user.LastName,
            PhoneNumber = user.PhoneNumber,
            Role = currentRole
        };

        await LoadAvailableRolesAsync();

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string id)
    {
        UserId = id;

        var user = await _userManager.FindByIdAsync(id);
        if (user == null)
        {
            return NotFound();
        }

        var currentUser = await _userManager.GetUserAsync(User);
        IsEditingSelf = currentUser?.Id == user.Id;

        await LoadAvailableRolesAsync();

        if (!ModelState.IsValid)
        {
            Input.Username = user.UserName ?? string.Empty;
            return Page();
        }

        // US0010-AC3: Cannot change own role
        if (IsEditingSelf && user.Email != Input.Email)
        {
            // Allow email change for self
        }

        // Check if email is already in use by another user
        var existingEmail = await _userManager.FindByEmailAsync(Input.Email);
        if (existingEmail != null && existingEmail.Id != user.Id)
        {
            ErrorMessage = "Email already in use";
            Input.Username = user.UserName ?? string.Empty;
            return Page();
        }

        var previousRoles = await _userManager.GetRolesAsync(user);
        var previousRole = previousRoles.FirstOrDefault();

        // Update user details
        user.Email = Input.Email;
        user.FirstName = Input.FirstName;
        user.LastName = Input.LastName;
        user.PhoneNumber = Input.PhoneNumber;
        user.UpdatedAt = DateTime.UtcNow;

        var result = await _userManager.UpdateAsync(user);

        if (!result.Succeeded)
        {
            ErrorMessage = string.Join(", ", result.Errors.Select(e => e.Description));
            _logger.LogError("Failed to update user {UserId}: {Errors}", id, ErrorMessage);
            Input.Username = user.UserName ?? string.Empty;
            return Page();
        }

        // Update role if changed and not editing self
        if (!IsEditingSelf && previousRole != Input.Role)
        {
            // US0010-AC4: Validate role change permissions
            var isSuperAdmin = currentUser != null && await _userManager.IsInRoleAsync(currentUser, "SuperAdmin");
            if (!isSuperAdmin && Input.Role == "SuperAdmin")
            {
                ErrorMessage = "You do not have permission to assign the Super Admin role";
                Input.Username = user.UserName ?? string.Empty;
                return Page();
            }

            if (previousRole != null)
            {
                await _userManager.RemoveFromRoleAsync(user, previousRole);
            }
            await _userManager.AddToRoleAsync(user, Input.Role);
        }

        // Audit log
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
        var userAgent = Request.Headers.UserAgent.ToString();

        await _auditService.LogAsync(
            action: "UserEdited",
            userId: user.Id,
            performedByUserId: currentUser?.Id,
            details: $"User '{user.UserName}' edited by {currentUser?.UserName}",
            ipAddress: ipAddress,
            userAgent: userAgent);

        _logger.LogInformation("User {Username} edited by {AdminUsername}", user.UserName, currentUser?.UserName);

        return RedirectToPage("/Admin/Users", new { StatusMessage = "User updated successfully" });
    }

    private async Task LoadAvailableRolesAsync()
    {
        var currentUser = await _userManager.GetUserAsync(User);
        var isSuperAdmin = currentUser != null && await _userManager.IsInRoleAsync(currentUser, "SuperAdmin");

        var allRoles = _roleManager.Roles.Select(r => r.Name).Where(r => r != null).Cast<string>().ToList();

        if (isSuperAdmin)
        {
            AvailableRoles = allRoles;
        }
        else
        {
            AvailableRoles = allRoles.Where(r => r != "SuperAdmin").ToList();
        }
    }
}
