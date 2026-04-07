using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SmartLog.Web.Data;
using SmartLog.Web.Data.Entities;
using SmartLog.Web.Services;
using SmartLog.Web.Validation;

namespace SmartLog.Web.Pages.Admin;

/// <summary>
/// Edit student page.
/// Implements US0016 (Edit Student Details).
/// </summary>
[Authorize(Policy = "CanManageStudents")]
public class EditStudentModel : PageModel
{
    private readonly ApplicationDbContext _context;
    private readonly IAuditService _auditService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IGradeSectionService _gradeSectionService;
    private readonly IAcademicYearService _academicYearService;
    private readonly IFileUploadService _fileUploadService;
    private readonly ILogger<EditStudentModel> _logger;

    public EditStudentModel(
        ApplicationDbContext context,
        IAuditService auditService,
        UserManager<ApplicationUser> userManager,
        IGradeSectionService gradeSectionService,
        IAcademicYearService academicYearService,
        IFileUploadService fileUploadService,
        ILogger<EditStudentModel> logger)
    {
        _context = context;
        _auditService = auditService;
        _userManager = userManager;
        _gradeSectionService = gradeSectionService;
        _academicYearService = academicYearService;
        _fileUploadService = fileUploadService;
        _logger = logger;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public Guid StudentId { get; set; }
    public string StudentIdDisplay { get; set; } = string.Empty;
    public string? CurrentProfilePicturePath { get; set; }
    public List<Section> Sections { get; set; } = new();
    public List<StudentEnrollment> EnrollmentHistory { get; set; } = new();
    public StudentEnrollment? CurrentEnrollment { get; set; }

    [TempData]
    public string? ErrorMessage { get; set; }

    [TempData]
    public string? StatusMessage { get; set; }

    public class InputModel
    {
        public string StudentId { get; set; } = string.Empty;

        [StringLength(12, MinimumLength = 12, ErrorMessage = "LRN must be exactly 12 digits")]
        [RegularExpression(@"^\d{12}$", ErrorMessage = "LRN must be exactly 12 digits")]
        [Display(Name = "LRN")]
        public string? LRN { get; set; }

        [Required(ErrorMessage = "First name is required")]
        [StringLength(100)]
        [Display(Name = "First Name")]
        public string FirstName { get; set; } = string.Empty;

        [StringLength(100)]
        [Display(Name = "Middle Name")]
        public string? MiddleName { get; set; }

        [Required(ErrorMessage = "Last name is required")]
        [StringLength(100)]
        [Display(Name = "Last Name")]
        public string LastName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Section is required")]
        [Display(Name = "Section")]
        public Guid SectionId { get; set; }

        [Required(ErrorMessage = "Parent/Guardian name is required")]
        [StringLength(200)]
        [Display(Name = "Parent/Guardian Name")]
        public string ParentGuardianName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Guardian relationship is required")]
        [StringLength(50)]
        [Display(Name = "Guardian Relationship")]
        public string GuardianRelationship { get; set; } = string.Empty;

        [Required(ErrorMessage = "Parent phone is required")]
        [PhMobile]
        [Display(Name = "Parent Phone")]
        public string ParentPhone { get; set; } = string.Empty;

        [PhMobile]
        [Display(Name = "Alternate Phone")]
        public string? AlternatePhone { get; set; }

        [Display(Name = "Profile Picture")]
        public IFormFile? ProfilePicture { get; set; }

        [Display(Name = "SMS Notifications Enabled")]
        public bool SmsEnabled { get; set; } = true;

        [Display(Name = "SMS Language")]
        public string SmsLanguage { get; set; } = "EN";
    }

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        var student = await _context.Students
            .Include(s => s.CurrentEnrollment)
                .ThenInclude(e => e!.Section)
                    .ThenInclude(s => s.GradeLevel)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (student == null)
        {
            return NotFound();
        }

        StudentId = id;
        StudentIdDisplay = student.StudentId;
        CurrentProfilePicturePath = student.ProfilePicturePath;

        // Load enrollment history
        EnrollmentHistory = await _gradeSectionService.GetStudentEnrollmentsAsync(id);
        CurrentEnrollment = student.CurrentEnrollment;

        // Load sections for the current grade level
        var currentAcademicYear = await _academicYearService.GetCurrentAcademicYearAsync();
        if (currentAcademicYear != null && student.CurrentEnrollment != null)
        {
            Sections = await _gradeSectionService.GetSectionsByGradeAsync(
                student.CurrentEnrollment.Section.GradeLevelId,
                activeOnly: true);
        }
        else
        {
            Sections = await _gradeSectionService.GetAllSectionsAsync(activeOnly: true);
        }

        Input = new InputModel
        {
            StudentId = student.StudentId,
            LRN = student.LRN,
            FirstName = student.FirstName,
            MiddleName = student.MiddleName,
            LastName = student.LastName,
            SectionId = student.CurrentEnrollment?.SectionId ?? Guid.Empty,
            ParentGuardianName = student.ParentGuardianName,
            GuardianRelationship = student.GuardianRelationship,
            ParentPhone = student.ParentPhone,
            AlternatePhone = student.AlternatePhone,
            SmsEnabled = student.SmsEnabled,
            SmsLanguage = student.SmsLanguage
        };

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(Guid id)
    {
        StudentId = id;

        var student = await _context.Students
            .Include(s => s.CurrentEnrollment)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (student == null)
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            Input.StudentId = student.StudentId;
            StudentIdDisplay = student.StudentId;
            Sections = await _gradeSectionService.GetAllSectionsAsync(activeOnly: true);
            EnrollmentHistory = await _gradeSectionService.GetStudentEnrollmentsAsync(id);
            return Page();
        }

        // Check if LRN is being changed and already exists
        if (!string.IsNullOrWhiteSpace(Input.LRN) &&
            Input.LRN != student.LRN &&
            _context.Students.Any(s => s.LRN == Input.LRN && s.Id != id))
        {
            ErrorMessage = "LRN already registered to another student";
            Input.StudentId = student.StudentId;
            StudentIdDisplay = student.StudentId;
            Sections = await _gradeSectionService.GetAllSectionsAsync(activeOnly: true);
            EnrollmentHistory = await _gradeSectionService.GetStudentEnrollmentsAsync(id);
            return Page();
        }

        // Update basic student info
        student.LRN = string.IsNullOrWhiteSpace(Input.LRN) ? null : Input.LRN;
        student.FirstName = Input.FirstName;
        student.MiddleName = string.IsNullOrWhiteSpace(Input.MiddleName) ? null : Input.MiddleName;
        student.LastName = Input.LastName;
        student.ParentGuardianName = Input.ParentGuardianName;
        student.GuardianRelationship = Input.GuardianRelationship;
        student.ParentPhone = Input.ParentPhone;
        student.AlternatePhone = string.IsNullOrWhiteSpace(Input.AlternatePhone) ? null : Input.AlternatePhone;
        student.SmsEnabled = Input.SmsEnabled;
        student.SmsLanguage = Input.SmsLanguage;
        student.UpdatedAt = DateTime.UtcNow;

        // Handle profile picture upload/replacement
        if (Input.ProfilePicture != null)
        {
            try
            {
                // Delete old picture if exists
                if (!string.IsNullOrEmpty(student.ProfilePicturePath))
                {
                    await _fileUploadService.DeleteProfilePictureAsync(student.ProfilePicturePath);
                }

                // Upload new picture
                var filePath = await _fileUploadService.UploadProfilePictureAsync(
                    Input.ProfilePicture, "students", student.Id.ToString());
                student.ProfilePicturePath = filePath;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to upload profile picture for student {StudentId}", student.StudentId);
                // Continue with update even if upload fails
            }
        }

        // Check if section has changed
        var currentAcademicYear = await _academicYearService.GetCurrentAcademicYearAsync();
        if (currentAcademicYear != null &&
            student.CurrentEnrollment != null &&
            student.CurrentEnrollment.SectionId != Input.SectionId)
        {
            // Transfer student to new section
            try
            {
                var newEnrollment = await _gradeSectionService.TransferStudentAsync(
                    id,
                    Input.SectionId,
                    currentAcademicYear.Id);

                // Update denormalized fields
                var newSection = await _gradeSectionService.GetSectionByIdAsync(Input.SectionId);
                if (newSection != null)
                {
                    student.GradeLevel = newSection.GradeLevel.Code;
                    student.Section = newSection.Name;
                }

                await _auditService.LogAsync(
                    action: "StudentTransferred",
                    userId: null,
                    performedByUserId: _userManager.GetUserId(User),
                    details: $"Student '{student.FullName}' transferred to section '{newSection?.Name}'",
                    ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString(),
                    userAgent: Request.Headers.UserAgent.ToString());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error transferring student");
                ErrorMessage = $"Error transferring student: {ex.Message}";
                Input.StudentId = student.StudentId;
                StudentIdDisplay = student.StudentId;
                Sections = await _gradeSectionService.GetAllSectionsAsync(activeOnly: true);
                EnrollmentHistory = await _gradeSectionService.GetStudentEnrollmentsAsync(id);
                return Page();
            }
        }

        await _context.SaveChangesAsync();

        // Audit log
        var currentUserId = _userManager.GetUserId(User);
        await _auditService.LogAsync(
            action: "StudentEdited",
            userId: null,
            performedByUserId: currentUserId,
            details: $"Student '{student.FullName}' (ID: {student.StudentId}) edited by {User.Identity?.Name}",
            ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString(),
            userAgent: Request.Headers.UserAgent.ToString());

        _logger.LogInformation("Student {StudentId} edited by {User}",
            student.StudentId, User.Identity?.Name);

        return RedirectToPage("/Admin/StudentDetails", new
        {
            id = student.Id,
            StatusMessage = "Student updated successfully"
        });
    }
}
