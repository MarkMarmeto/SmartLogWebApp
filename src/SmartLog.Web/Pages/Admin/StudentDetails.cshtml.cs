using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SmartLog.Web.Data;
using SmartLog.Web.Data.Entities;
using SmartLog.Web.Services;
using SmartLog.Web.Services.Sms;

namespace SmartLog.Web.Pages.Admin;

/// <summary>
/// Student details page with QR code display and management.
/// Implements US0019-AC5 (View QR Code), US0020 (Regenerate QR), US0017 (Deactivate/Reactivate), US0056 (Personal SMS).
/// </summary>
[Authorize(Policy = "CanViewStudents")]
public class StudentDetailsModel : PageModel
{
    private readonly ApplicationDbContext _context;
    private readonly IQrCodeService _qrCodeService;
    private readonly IAuditService _auditService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IFileUploadService _fileUploadService;
    private readonly IAuthorizationService _authorizationService;
    private readonly ISmsService _smsService;
    private readonly ILogger<StudentDetailsModel> _logger;

    public StudentDetailsModel(
        ApplicationDbContext context,
        IQrCodeService qrCodeService,
        IAuditService auditService,
        UserManager<ApplicationUser> userManager,
        IFileUploadService fileUploadService,
        IAuthorizationService authorizationService,
        ISmsService smsService,
        ILogger<StudentDetailsModel> logger)
    {
        _context = context;
        _qrCodeService = qrCodeService;
        _auditService = auditService;
        _userManager = userManager;
        _fileUploadService = fileUploadService;
        _authorizationService = authorizationService;
        _smsService = smsService;
        _logger = logger;
    }

    public Student Student { get; set; } = null!;
    public QrCode? QrCode => Student?.QrCodes.FirstOrDefault(q => q.IsValid);
    public string ProfilePictureUrl { get; set; } = string.Empty;

    [TempData]
    public string? StatusMessage { get; set; }

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        var student = await _context.Students
            .Include(s => s.QrCodes)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (student == null)
        {
            return NotFound();
        }

        Student = student;
        ProfilePictureUrl = _fileUploadService.GetProfilePictureUrl(student.ProfilePicturePath);

        return Page();
    }

    /// <summary>
    /// US0020: Regenerate QR code (invalidates old code).
    /// </summary>
    public async Task<IActionResult> OnPostRegenerateQrAsync(Guid studentId)
    {
        var authResult = await _authorizationService.AuthorizeAsync(User, "CanManageStudents");
        if (!authResult.Succeeded)
        {
            return Forbid();
        }

        var student = await _context.Students
            .Include(s => s.QrCodes)
            .FirstOrDefaultAsync(s => s.Id == studentId);

        if (student == null)
        {
            return NotFound();
        }

        // Generate new QR code first so we have its ID
        var newQrCode = await _qrCodeService.GenerateQrCodeAsync(student.StudentId);
        newQrCode.StudentId = student.Id;
        _context.QrCodes.Add(newQrCode);
        await _context.SaveChangesAsync();

        // Invalidate old QR codes — keep records for audit trail (US0079)
        var oldQrCode = student.QrCodes.FirstOrDefault(q => q.IsValid && q.Id != newQrCode.Id);
        string? oldQrPayload = null;
        if (oldQrCode != null)
        {
            oldQrPayload = oldQrCode.Payload;
            oldQrCode.IsValid = false;
            oldQrCode.InvalidatedAt = DateTime.UtcNow;
            oldQrCode.ReplacedByQrCodeId = newQrCode.Id;
            await _context.SaveChangesAsync();
        }

        var currentUser = User.Identity?.Name;
        var currentUserId = _userManager.GetUserId(User);
        await _auditService.LogAsync(
            action: "QrCodeRegenerated",
            userId: null,
            performedByUserId: currentUserId,
            details: $"QR code regenerated for student '{student.FullName}' (ID: {student.StudentId}) by {currentUser}. Previous QR invalidated: {oldQrPayload ?? "none"}",
            ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString(),
            userAgent: Request.Headers.UserAgent.ToString());

        _logger.LogInformation("QR code regenerated for student {StudentId} by {User}",
            student.StudentId, currentUser);

        StatusMessage = "QR code regenerated successfully";
        return RedirectToPage(new { id = studentId });
    }

    /// <summary>
    /// US0081: Invalidate QR code without regenerating a new one (lost card, not yet replaced).
    /// Old QR marked IsValid=false with audit trail. No new QR is created.
    /// </summary>
    public async Task<IActionResult> OnPostInvalidateQrAsync(Guid studentId)
    {
        var authResult = await _authorizationService.AuthorizeAsync(User, "CanManageStudents");
        if (!authResult.Succeeded)
        {
            return Forbid();
        }

        var student = await _context.Students
            .Include(s => s.QrCodes)
            .FirstOrDefaultAsync(s => s.Id == studentId);

        if (student == null)
        {
            return NotFound();
        }

        var activeQrCode = student.QrCodes.FirstOrDefault(q => q.IsValid);
        if (activeQrCode == null)
        {
            StatusMessage = "No active QR code to invalidate.";
            return RedirectToPage(new { id = studentId });
        }

        activeQrCode.IsValid = false;
        activeQrCode.InvalidatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        var currentUser = User.Identity?.Name;
        var currentUserId = _userManager.GetUserId(User);
        await _auditService.LogAsync(
            action: "QrCodeInvalidated",
            userId: null,
            performedByUserId: currentUserId,
            details: $"QR code invalidated (no replacement) for student '{student.FullName}' (ID: {student.StudentId}) by {currentUser}. QR payload: {activeQrCode.Payload}",
            ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString(),
            userAgent: Request.Headers.UserAgent.ToString());

        _logger.LogInformation("QR code invalidated (no replacement) for student {StudentId} by {User}",
            student.StudentId, currentUser);

        StatusMessage = "QR code invalidated. The student will need a new card printed.";
        return RedirectToPage(new { id = studentId });
    }

    /// <summary>
    /// US0017-AC1: Deactivate student.
    /// </summary>
    public async Task<IActionResult> OnPostDeactivateAsync(Guid studentId)
    {
        var authResult = await _authorizationService.AuthorizeAsync(User, "CanManageStudents");
        if (!authResult.Succeeded)
        {
            return Forbid();
        }

        var student = await _context.Students.FindAsync(studentId);
        if (student == null)
        {
            return NotFound();
        }

        student.IsActive = false;
        student.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        // Audit log
        var currentUser = User.Identity?.Name;
        var currentUserId = _userManager.GetUserId(User);
        await _auditService.LogAsync(
            action: "StudentDeactivated",
            userId: null,
            performedByUserId: currentUserId,
            details: $"Student '{student.FullName}' (ID: {student.StudentId}) deactivated by {currentUser}",
            ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString(),
            userAgent: Request.Headers.UserAgent.ToString());

        _logger.LogInformation("Student {StudentId} deactivated by {User}",
            student.StudentId, currentUser);

        StatusMessage = "Student deactivated successfully";
        return RedirectToPage(new { id = studentId });
    }

    /// <summary>
    /// US0017-AC3: Reactivate student.
    /// </summary>
    public async Task<IActionResult> OnPostReactivateAsync(Guid studentId)
    {
        var authResult = await _authorizationService.AuthorizeAsync(User, "CanManageStudents");
        if (!authResult.Succeeded)
        {
            return Forbid();
        }

        var student = await _context.Students.FindAsync(studentId);
        if (student == null)
        {
            return NotFound();
        }

        student.IsActive = true;
        student.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        // Audit log
        var currentUser = User.Identity?.Name;
        var currentUserId = _userManager.GetUserId(User);
        await _auditService.LogAsync(
            action: "StudentReactivated",
            userId: null,
            performedByUserId: currentUserId,
            details: $"Student '{student.FullName}' (ID: {student.StudentId}) reactivated by {currentUser}",
            ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString(),
            userAgent: Request.Headers.UserAgent.ToString());

        _logger.LogInformation("Student {StudentId} reactivated by {User}",
            student.StudentId, currentUser);

        StatusMessage = "Student reactivated successfully";
        return RedirectToPage(new { id = studentId });
    }

    /// <summary>
    /// US0056: Send a personal freeform SMS to the student's parent(s).
    /// Queued with MessageType="PERSONAL". Sends to both ParentPhone and AlternatePhone if present.
    /// </summary>
    public async Task<IActionResult> OnPostSendPersonalSmsAsync(
        [FromBody] PersonalSmsRequest request)
    {
        var authResult = await _authorizationService.AuthorizeAsync(User, "CanManageStudents");
        if (!authResult.Succeeded)
        {
            return new JsonResult(new { success = false, error = "Forbidden" }) { StatusCode = 403 };
        }

        if (string.IsNullOrWhiteSpace(request.Message) || request.Message.Length > 320)
        {
            return new JsonResult(new { success = false, error = "Message must be 1–320 characters." });
        }

        var student = await _context.Students.FindAsync(request.StudentId);
        if (student == null)
        {
            return new JsonResult(new { success = false, error = "Student not found." });
        }

        if (string.IsNullOrWhiteSpace(student.ParentPhone))
        {
            return new JsonResult(new { success = false, error = "No parent phone on record." });
        }

        try
        {
            int queued = 0;

            await _smsService.QueueCustomSmsAsync(
                student.ParentPhone,
                request.Message,
                SmsPriority.Normal,
                "PERSONAL");
            queued++;

            if (!string.IsNullOrWhiteSpace(student.AlternatePhone))
            {
                await _smsService.QueueCustomSmsAsync(
                    student.AlternatePhone,
                    request.Message,
                    SmsPriority.Normal,
                    "PERSONAL");
                queued++;
            }

            _logger.LogInformation(
                "Personal SMS queued for student {StudentId} ({Queued} recipient(s)) by {User}",
                student.StudentId, queued, User.Identity?.Name);

            return new JsonResult(new { success = true, queued });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error queuing personal SMS for student {StudentId}", student.StudentId);
            return new JsonResult(new { success = false, error = "Failed to queue SMS." });
        }
    }
}

/// <summary>US0056: Request body for personal SMS.</summary>
public class PersonalSmsRequest
{
    public Guid StudentId { get; set; }
    public string Message { get; set; } = string.Empty;
}
