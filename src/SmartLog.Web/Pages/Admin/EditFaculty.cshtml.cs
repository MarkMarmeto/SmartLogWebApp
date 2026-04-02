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
/// Edit faculty record page.
/// Implements US0024 (Edit Faculty Details) and US0027 (Link/Unlink Faculty to User Account).
/// </summary>
[Authorize(Policy = "CanManageUsers")]
public class EditFacultyModel : PageModel
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IAuditService _auditService;
    private readonly IFileUploadService _fileUploadService;
    private readonly ILogger<EditFacultyModel> _logger;

    public EditFacultyModel(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        IAuditService auditService,
        IFileUploadService fileUploadService,
        ILogger<EditFacultyModel> logger)
    {
        _context = context;
        _userManager = userManager;
        _auditService = auditService;
        _fileUploadService = fileUploadService;
        _logger = logger;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public Faculty Faculty { get; set; } = null!;
    public string? CurrentProfilePicturePath { get; set; }
    public List<string> Departments { get; set; } = new();
    public List<SelectListItem> AvailableUsers { get; set; } = new();

    [TempData]
    public string? StatusMessage { get; set; }

    public class InputModel
    {
        public Guid Id { get; set; }

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

        [StringLength(50)]
        [Display(Name = "External Employee ID")]
        public string? ExternalEmployeeId { get; set; }

        [Display(Name = "Link to User Account")]
        public string? UserId { get; set; }

        [Display(Name = "Profile Picture")]
        public IFormFile? ProfilePicture { get; set; }
    }

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        var faculty = await _context.Faculties
            .Include(f => f.User)
            .FirstOrDefaultAsync(f => f.Id == id);

        if (faculty == null)
        {
            return NotFound();
        }

        Faculty = faculty;
        CurrentProfilePicturePath = faculty.ProfilePicturePath;

        Input = new InputModel
        {
            Id = faculty.Id,
            FirstName = faculty.FirstName,
            LastName = faculty.LastName,
            Department = faculty.Department,
            Position = faculty.Position,
            Email = faculty.Email,
            PhoneNumber = faculty.PhoneNumber,
            HireDate = faculty.HireDate,
            ExternalEmployeeId = faculty.ExternalEmployeeId,
            UserId = faculty.UserId
        };

        await LoadLookupsAsync(faculty.Id, faculty.UserId);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var faculty = await _context.Faculties
            .Include(f => f.User)
            .FirstOrDefaultAsync(f => f.Id == Input.Id);

        if (faculty == null)
        {
            return NotFound();
        }

        Faculty = faculty;
        await LoadLookupsAsync(faculty.Id, faculty.UserId);

        if (!ModelState.IsValid)
        {
            return Page();
        }

        // Check if new UserId is already linked to another faculty member (US0027-AC2)
        if (!string.IsNullOrWhiteSpace(Input.UserId) && Input.UserId != faculty.UserId)
        {
            var userAlreadyLinked = await _context.Faculties
                .AnyAsync(f => f.UserId == Input.UserId && f.Id != faculty.Id);

            if (userAlreadyLinked)
            {
                ModelState.AddModelError("Input.UserId", "This user is already linked to another faculty member.");
                return Page();
            }
        }

        var oldUserId = faculty.UserId;
        var oldUserName = faculty.User?.UserName;

        faculty.FirstName = Input.FirstName;
        faculty.LastName = Input.LastName;
        faculty.Department = Input.Department;
        faculty.Position = Input.Position;
        faculty.Email = Input.Email;
        faculty.PhoneNumber = Input.PhoneNumber;
        faculty.HireDate = Input.HireDate;
        faculty.ExternalEmployeeId = string.IsNullOrWhiteSpace(Input.ExternalEmployeeId) ? null : Input.ExternalEmployeeId;
        faculty.UserId = string.IsNullOrWhiteSpace(Input.UserId) ? null : Input.UserId;

        // Handle profile picture upload/replacement
        if (Input.ProfilePicture != null)
        {
            try
            {
                // Delete old picture if exists
                if (!string.IsNullOrEmpty(faculty.ProfilePicturePath))
                {
                    await _fileUploadService.DeleteProfilePictureAsync(faculty.ProfilePicturePath);
                }

                // Upload new picture
                var filePath = await _fileUploadService.UploadProfilePictureAsync(
                    Input.ProfilePicture, "faculty", faculty.Id.ToString());
                faculty.ProfilePicturePath = filePath;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to upload profile picture for faculty {EmployeeId}", faculty.EmployeeId);
                // Continue with update even if upload fails
            }
        }

        await _context.SaveChangesAsync();

        // Audit log
        var currentUser = await _userManager.GetUserAsync(User);
        var details = $"Updated faculty: {faculty.FullName} (ID: {faculty.EmployeeId})";

        // Log user account link/unlink changes (US0027)
        if (oldUserId != faculty.UserId)
        {
            if (oldUserId != null && faculty.UserId == null)
            {
                details += $"; Unlinked from user {oldUserName}";
            }
            else if (oldUserId == null && faculty.UserId != null)
            {
                var newUser = await _userManager.FindByIdAsync(faculty.UserId);
                details += $"; Linked to user {newUser?.UserName}";
            }
            else if (oldUserId != null && faculty.UserId != null)
            {
                var newUser = await _userManager.FindByIdAsync(faculty.UserId);
                details += $"; Changed link from user {oldUserName} to {newUser?.UserName}";
            }
        }

        await _auditService.LogAsync(
            action: "UpdateFaculty",
            userId: faculty.UserId,
            performedByUserId: currentUser?.Id,
            details: details,
            ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString());

        StatusMessage = $"Faculty member {faculty.FullName} updated successfully.";
        return RedirectToPage("/Admin/FacultyDetails", new { id = faculty.Id });
    }

    private async Task LoadLookupsAsync(Guid facultyId, string? currentUserId)
    {
        // Predefined departments
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

        // Get users not already linked to other faculty members (US0027-AC2)
        var linkedUserIds = await _context.Faculties
            .Where(f => f.UserId != null && f.Id != facultyId)
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
                Text = $"{u.FirstName} {u.LastName} ({u.UserName})",
                Selected = u.Id == currentUserId
            })
            .ToList();

        AvailableUsers.Insert(0, new SelectListItem
        {
            Value = "",
            Text = "-- No User Link --",
            Selected = string.IsNullOrEmpty(currentUserId)
        });
    }
}
