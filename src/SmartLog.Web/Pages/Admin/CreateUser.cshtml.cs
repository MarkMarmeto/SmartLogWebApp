using System.ComponentModel.DataAnnotations;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SmartLog.Web.Data.Entities;
using SmartLog.Web.Services;

namespace SmartLog.Web.Pages.Admin;

/// <summary>
/// Create new user page.
/// Implements US0009 (Create User Account).
/// </summary>
[Authorize(Policy = "CanManageUsers")]
public class CreateUserModel : PageModel
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly IAuditService _auditService;
    private readonly ILogger<CreateUserModel> _logger;

    public CreateUserModel(
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        IAuditService auditService,
        ILogger<CreateUserModel> logger)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _auditService = auditService;
        _logger = logger;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public List<string> AvailableRoles { get; set; } = new();

    [TempData]
    public string? ErrorMessage { get; set; }

    public string? GeneratedPassword { get; set; }

    public class InputModel
    {
        [Required(ErrorMessage = "Username is required")]
        [StringLength(50, MinimumLength = 3, ErrorMessage = "Username must be between 3 and 50 characters")]
        [RegularExpression(@"^[a-z0-9._]+$", ErrorMessage = "Username can only contain lowercase letters, numbers, dots, and underscores")]
        [Display(Name = "Username")]
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

    public async Task OnGetAsync()
    {
        await LoadAvailableRolesAsync();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await LoadAvailableRolesAsync();

        if (!ModelState.IsValid)
        {
            return Page();
        }

        // US0009-AC5: Validate role is in available list
        if (!AvailableRoles.Contains(Input.Role))
        {
            ErrorMessage = "Invalid role selected";
            return Page();
        }

        // US0009-AC3: Check if username already exists
        var existingUser = await _userManager.FindByNameAsync(Input.Username);
        if (existingUser != null)
        {
            ErrorMessage = "Username already exists";
            return Page();
        }

        // Check if email already exists
        var existingEmail = await _userManager.FindByEmailAsync(Input.Email);
        if (existingEmail != null)
        {
            ErrorMessage = "Email already registered";
            return Page();
        }

        // Generate temporary password
        var tempPassword = GenerateTemporaryPassword();

        // Create user
        var user = new ApplicationUser
        {
            UserName = Input.Username,
            Email = Input.Email,
            FirstName = Input.FirstName,
            LastName = Input.LastName,
            PhoneNumber = Input.PhoneNumber,
            EmailConfirmed = true,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var result = await _userManager.CreateAsync(user, tempPassword);

        if (!result.Succeeded)
        {
            ErrorMessage = string.Join(", ", result.Errors.Select(e => e.Description));
            _logger.LogError("Failed to create user {Username}: {Errors}", Input.Username, ErrorMessage);
            return Page();
        }

        // Assign role
        await _userManager.AddToRoleAsync(user, Input.Role);

        // Audit log
        var currentUser = await _userManager.GetUserAsync(User);
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
        var userAgent = Request.Headers.UserAgent.ToString();

        await _auditService.LogAsync(
            action: "UserCreated",
            userId: user.Id,
            performedByUserId: currentUser?.Id,
            details: $"User '{Input.Username}' created with role '{Input.Role}'",
            ipAddress: ipAddress,
            userAgent: userAgent);

        _logger.LogInformation("User {Username} created by {AdminUsername}", Input.Username, currentUser?.UserName);

        // US0009-AC2: Show temporary password
        GeneratedPassword = tempPassword;

        return Page();
    }

    private async Task LoadAvailableRolesAsync()
    {
        var currentUser = await _userManager.GetUserAsync(User);
        var isSuperAdmin = currentUser != null && await _userManager.IsInRoleAsync(currentUser, "SuperAdmin");

        // US0009-AC5: Admin can only assign Admin, Teacher, Security, Staff
        // Super Admin can assign any role
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

    private string GenerateTemporaryPassword()
    {
        // Generate secure temporary password: 12 chars with uppercase, lowercase, digit, special char
        const string lowercase = "abcdefghijklmnopqrstuvwxyz";
        const string uppercase = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        const string digits = "0123456789";
        const string special = "!@#$%";
        const string all = lowercase + uppercase + digits + special;

        var random = new Random();
        var password = new StringBuilder();

        // Ensure at least one of each required type
        password.Append(uppercase[random.Next(uppercase.Length)]);
        password.Append(lowercase[random.Next(lowercase.Length)]);
        password.Append(digits[random.Next(digits.Length)]);
        password.Append(special[random.Next(special.Length)]);

        // Fill remaining with random characters
        for (int i = 4; i < 12; i++)
        {
            password.Append(all[random.Next(all.Length)]);
        }

        // Shuffle the password
        var chars = password.ToString().ToCharArray();
        for (int i = chars.Length - 1; i > 0; i--)
        {
            int j = random.Next(i + 1);
            (chars[i], chars[j]) = (chars[j], chars[i]);
        }

        return new string(chars);
    }
}
