using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SmartLog.Web.Data;
using SmartLog.Web.Data.Entities;
using SmartLog.Web.Services;

namespace SmartLog.Web.Pages.Admin;

/// <summary>
/// Create student page with automatic QR code generation.
/// Implements US0015 (Create Student Record) and US0019 (Generate QR Code).
/// </summary>
[Authorize(Policy = "CanManageStudents")]
public class CreateStudentModel : PageModel
{
    private readonly ApplicationDbContext _context;
    private readonly IQrCodeService _qrCodeService;
    private readonly IAuditService _auditService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IIdGenerationService _idGenerationService;
    private readonly IGradeSectionService _gradeSectionService;
    private readonly IAcademicYearService _academicYearService;
    private readonly IFileUploadService _fileUploadService;
    private readonly ILogger<CreateStudentModel> _logger;

    public CreateStudentModel(
        ApplicationDbContext context,
        IQrCodeService qrCodeService,
        IAuditService auditService,
        UserManager<ApplicationUser> userManager,
        IIdGenerationService idGenerationService,
        IGradeSectionService gradeSectionService,
        IAcademicYearService academicYearService,
        IFileUploadService fileUploadService,
        ILogger<CreateStudentModel> logger)
    {
        _context = context;
        _qrCodeService = qrCodeService;
        _auditService = auditService;
        _userManager = userManager;
        _idGenerationService = idGenerationService;
        _gradeSectionService = gradeSectionService;
        _academicYearService = academicYearService;
        _fileUploadService = fileUploadService;
        _logger = logger;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public List<GradeLevel> GradeLevels { get; set; } = new();
    public List<Section> Sections { get; set; } = new();

    [TempData]
    public string? ErrorMessage { get; set; }

    public class InputModel
    {
        // Student ID will be auto-generated, no longer an input field

        [StringLength(12, MinimumLength = 12, ErrorMessage = "LRN must be exactly 12 digits")]
        [RegularExpression(@"^\d{12}$", ErrorMessage = "LRN must be exactly 12 digits")]
        [Display(Name = "LRN (Learner Reference Number)")]
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

        [Required(ErrorMessage = "Grade level is required")]
        [Display(Name = "Grade Level")]
        public int GradeLevelId { get; set; }

        [Required(ErrorMessage = "Section is required")]
        [Display(Name = "Section")]
        public int SectionId { get; set; }

        [Required(ErrorMessage = "Parent/Guardian name is required")]
        [StringLength(200)]
        [Display(Name = "Parent/Guardian Name")]
        public string ParentGuardianName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Guardian relationship is required")]
        [StringLength(50)]
        [Display(Name = "Guardian Relationship")]
        public string GuardianRelationship { get; set; } = string.Empty;

        [Required(ErrorMessage = "Parent phone is required")]
        [Phone(ErrorMessage = "Please enter a valid phone number")]
        [StringLength(20)]
        [Display(Name = "Parent Phone")]
        public string ParentPhone { get; set; } = string.Empty;

        [Display(Name = "Profile Picture")]
        public IFormFile? ProfilePicture { get; set; }
    }

    public async Task OnGetAsync()
    {
        GradeLevels = await _gradeSectionService.GetAllGradeLevelsAsync(activeOnly: true);
        Sections = await _gradeSectionService.GetAllSectionsAsync(activeOnly: true);
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            GradeLevels = await _gradeSectionService.GetAllGradeLevelsAsync(activeOnly: true);
            Sections = await _gradeSectionService.GetAllSectionsAsync(activeOnly: true);
            return Page();
        }

        // US0015-AC2b: Check if LRN already exists (if provided)
        if (!string.IsNullOrWhiteSpace(Input.LRN) && _context.Students.Any(s => s.LRN == Input.LRN))
        {
            ErrorMessage = "LRN already registered to another student";
            GradeLevels = await _gradeSectionService.GetAllGradeLevelsAsync(activeOnly: true);
            Sections = await _gradeSectionService.GetAllSectionsAsync(activeOnly: true);
            return Page();
        }

        // Get the grade level and section for student ID generation and enrollment
        var gradeLevel = await _gradeSectionService.GetGradeLevelByIdAsync(Input.GradeLevelId);
        var section = await _gradeSectionService.GetSectionByIdAsync(Input.SectionId);
        var currentAcademicYear = await _academicYearService.GetCurrentAcademicYearAsync();

        if (gradeLevel == null || section == null || currentAcademicYear == null)
        {
            ErrorMessage = "Invalid grade level, section, or no current academic year found.";
            GradeLevels = await _gradeSectionService.GetAllGradeLevelsAsync(activeOnly: true);
            Sections = await _gradeSectionService.GetAllSectionsAsync(activeOnly: true);
            return Page();
        }

        // Auto-generate Student ID
        var studentId = await _idGenerationService.GenerateStudentIdAsync(gradeLevel.Code);

        // Create student
        var student = new Student
        {
            StudentId = studentId,
            LRN = string.IsNullOrWhiteSpace(Input.LRN) ? null : Input.LRN,
            FirstName = Input.FirstName,
            MiddleName = string.IsNullOrWhiteSpace(Input.MiddleName) ? null : Input.MiddleName,
            LastName = Input.LastName,
            GradeLevel = gradeLevel.Code,
            Section = section.Name,
            ParentGuardianName = Input.ParentGuardianName,
            GuardianRelationship = Input.GuardianRelationship,
            ParentPhone = Input.ParentPhone,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Students.Add(student);
        await _context.SaveChangesAsync();

        // Upload profile picture if provided
        if (Input.ProfilePicture != null)
        {
            try
            {
                var filePath = await _fileUploadService.UploadProfilePictureAsync(
                    Input.ProfilePicture, "students", student.Id.ToString());
                student.ProfilePicturePath = filePath;
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to upload profile picture for student {StudentId}", student.StudentId);
                // Continue with student creation even if upload fails
            }
        }

        // Create enrollment record
        var enrollment = await _gradeSectionService.EnrollStudentAsync(
            student.Id,
            Input.SectionId,
            currentAcademicYear.Id);

        // US0019-AC1: Automatically generate QR code
        var qrCode = await _qrCodeService.GenerateQrCodeAsync(student.StudentId);
        qrCode.StudentId = student.Id;
        _context.QrCodes.Add(qrCode);
        await _context.SaveChangesAsync();

        // US0015-AC6: Audit log
        var currentUserId = _userManager.GetUserId(User);
        await _auditService.LogAsync(
            action: "StudentCreated",
            userId: null,
            performedByUserId: currentUserId,
            details: $"Student '{student.FullName}' (ID: {student.StudentId}) created by {User.Identity?.Name}",
            ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString(),
            userAgent: Request.Headers.UserAgent.ToString());

        _logger.LogInformation("Student {StudentId} created with QR code by {User}",
            student.StudentId, User.Identity?.Name);

        // US0015-AC2: Redirect to student details page with success message
        return RedirectToPage("/Admin/StudentDetails", new
        {
            id = student.Id,
            StatusMessage = $"Student '{student.FullName}' created successfully"
        });
    }
}
