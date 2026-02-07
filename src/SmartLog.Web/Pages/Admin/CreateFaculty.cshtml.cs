using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SmartLog.Web.Data;
using SmartLog.Web.Data.Entities;
using SmartLog.Web.Services;
using System.ComponentModel.DataAnnotations;

namespace SmartLog.Web.Pages.Admin;

/// <summary>
/// Create faculty record page.
/// Implements US0023 (Create Faculty Record).
/// </summary>
[Authorize(Policy = "CanManageUsers")]
public class CreateFacultyModel : PageModel
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IAuditService _auditService;
    private readonly IIdGenerationService _idGenerationService;
    private readonly IFileUploadService _fileUploadService;
    private readonly ILogger<CreateFacultyModel> _logger;

    public CreateFacultyModel(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        IAuditService auditService,
        IIdGenerationService idGenerationService,
        IFileUploadService fileUploadService,
        ILogger<CreateFacultyModel> logger)
    {
        _context = context;
        _userManager = userManager;
        _auditService = auditService;
        _idGenerationService = idGenerationService;
        _fileUploadService = fileUploadService;
        _logger = logger;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public List<string> Departments { get; set; } = new();
    public List<SelectListItem> AvailableUsers { get; set; } = new();

    [TempData]
    public string? StatusMessage { get; set; }

    public class InputModel
    {
        // EmployeeId will be auto-generated

        [StringLength(50)]
        [Display(Name = "External Employee ID (Optional)")]
        public string? ExternalEmployeeId { get; set; }

        [Required]
        [StringLength(100)]
        [Display(Name = "First Name")]
        public string FirstName { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        [Display(Name = "Last Name")]
        public string LastName { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string Department { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string Position { get; set; } = string.Empty;

        [EmailAddress]
        [StringLength(256)]
        public string? Email { get; set; }

        [Phone]
        [StringLength(20)]
        [Display(Name = "Phone Number")]
        public string? PhoneNumber { get; set; }

        [Display(Name = "Hire Date")]
        public DateTime? HireDate { get; set; }

        [Display(Name = "Link to User Account (Optional)")]
        public string? UserId { get; set; }

        [Display(Name = "Profile Picture")]
        public IFormFile? ProfilePicture { get; set; }
    }

    public async Task<IActionResult> OnGetAsync()
    {
        await LoadLookupsAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await LoadLookupsAsync();

        if (!ModelState.IsValid)
        {
            return Page();
        }

        // Auto-generate Employee ID
        var employeeId = await _idGenerationService.GenerateEmployeeIdAsync();

        // Check if UserId is already linked to another faculty member (US0023-AC6)
        if (!string.IsNullOrWhiteSpace(Input.UserId))
        {
            var userAlreadyLinked = await _context.Faculties
                .AnyAsync(f => f.UserId == Input.UserId);

            if (userAlreadyLinked)
            {
                ModelState.AddModelError("Input.UserId", "This user is already linked to another faculty member.");
                return Page();
            }
        }

        var faculty = new Faculty
        {
            EmployeeId = employeeId,
            ExternalEmployeeId = string.IsNullOrWhiteSpace(Input.ExternalEmployeeId) ? null : Input.ExternalEmployeeId,
            FirstName = Input.FirstName,
            LastName = Input.LastName,
            Department = Input.Department,
            Position = Input.Position,
            Email = Input.Email,
            PhoneNumber = Input.PhoneNumber,
            HireDate = Input.HireDate,
            UserId = string.IsNullOrWhiteSpace(Input.UserId) ? null : Input.UserId,
            IsActive = true
        };

        _context.Faculties.Add(faculty);
        await _context.SaveChangesAsync();

        // Upload profile picture if provided
        if (Input.ProfilePicture != null)
        {
            try
            {
                var filePath = await _fileUploadService.UploadProfilePictureAsync(
                    Input.ProfilePicture, "faculty", faculty.Id.ToString());
                faculty.ProfilePicturePath = filePath;
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to upload profile picture for faculty {EmployeeId}", faculty.EmployeeId);
                // Continue with faculty creation even if upload fails
            }
        }

        // Audit log
        var currentUser = await _userManager.GetUserAsync(User);
        await _auditService.LogAsync(
            action: "CreateFaculty",
            userId: faculty.UserId,
            performedByUserId: currentUser?.Id,
            details: $"Created faculty record for {faculty.FullName} (ID: {faculty.EmployeeId})",
            ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString());

        StatusMessage = $"Faculty member {faculty.FullName} created successfully.";
        return RedirectToPage("/Admin/Faculty");
    }

    private async Task LoadLookupsAsync()
    {
        // Predefined departments (US0023-AC5)
        Departments = new List<string>
        {
            "Mathematics",
            "Science",
            "English",
            "Filipino",
            "Social Studies",
            "Physical Education",
            "Arts",
            "Technology",
            "Administration",
            "Support Staff"
        };

        // Get users not already linked to faculty (US0023-AC6)
        var linkedUserIds = await _context.Faculties
            .Where(f => f.UserId != null)
            .Select(f => f.UserId)
            .ToListAsync();

        var availableUsers = await _userManager.Users
            .Where(u => u.IsActive && !linkedUserIds.Contains(u.Id))
            .OrderBy(u => u.LastName)
            .ThenBy(u => u.FirstName)
            .ToListAsync();

        AvailableUsers = availableUsers
            .Select(u => new SelectListItem
            {
                Value = u.Id,
                Text = $"{u.FirstName} {u.LastName} ({u.UserName})"
            })
            .ToList();

        AvailableUsers.Insert(0, new SelectListItem { Value = "", Text = "-- No User Link --" });
    }
}
