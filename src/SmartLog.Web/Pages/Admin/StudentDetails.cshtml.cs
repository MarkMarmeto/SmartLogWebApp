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
/// Student details page with QR code display and management.
/// Implements US0019-AC5 (View QR Code), US0020 (Regenerate QR), US0017 (Deactivate/Reactivate).
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
    private readonly ILogger<StudentDetailsModel> _logger;

    public StudentDetailsModel(
        ApplicationDbContext context,
        IQrCodeService qrCodeService,
        IAuditService auditService,
        UserManager<ApplicationUser> userManager,
        IFileUploadService fileUploadService,
        IAuthorizationService authorizationService,
        ILogger<StudentDetailsModel> logger)
    {
        _context = context;
        _qrCodeService = qrCodeService;
        _auditService = auditService;
        _userManager = userManager;
        _fileUploadService = fileUploadService;
        _authorizationService = authorizationService;
        _logger = logger;
    }

    public Student Student { get; set; } = null!;
    public QrCode? QrCode { get; set; }
    public string ProfilePictureUrl { get; set; } = string.Empty;

    [TempData]
    public string? StatusMessage { get; set; }

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        var student = await _context.Students
            .Include(s => s.QrCode)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (student == null)
        {
            return NotFound();
        }

        Student = student;
        QrCode = student.QrCode;
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
            .Include(s => s.QrCode)
            .FirstOrDefaultAsync(s => s.Id == studentId);

        if (student == null)
        {
            return NotFound();
        }

        // Invalidate old QR code
        if (student.QrCode != null)
        {
            student.QrCode.IsValid = false;
            _context.QrCodes.Remove(student.QrCode);
        }

        // Generate new QR code
        var newQrCode = await _qrCodeService.GenerateQrCodeAsync(student.StudentId);
        newQrCode.StudentId = student.Id;
        _context.QrCodes.Add(newQrCode);

        await _context.SaveChangesAsync();

        // Audit log
        var currentUser = User.Identity?.Name;
        var currentUserId = _userManager.GetUserId(User);
        await _auditService.LogAsync(
            action: "QrCodeRegenerated",
            userId: null,
            performedByUserId: currentUserId,
            details: $"QR code regenerated for student '{student.FullName}' (ID: {student.StudentId}) by {currentUser}",
            ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString(),
            userAgent: Request.Headers.UserAgent.ToString());

        _logger.LogInformation("QR code regenerated for student {StudentId} by {User}",
            student.StudentId, currentUser);

        StatusMessage = "QR code regenerated successfully";
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
}
