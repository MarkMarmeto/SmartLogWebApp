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
/// Faculty details page.
/// Implements US0025 (Deactivate/Reactivate Faculty) and US0027 (Link/Unlink Faculty to User Account).
/// </summary>
[Authorize(Policy = "CanViewFaculty")]
public class FacultyDetailsModel : PageModel
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IAuditService _auditService;
    private readonly IFileUploadService _fileUploadService;

    public FacultyDetailsModel(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        IAuditService auditService,
        IFileUploadService fileUploadService)
    {
        _context = context;
        _userManager = userManager;
        _auditService = auditService;
        _fileUploadService = fileUploadService;
    }

    public Faculty Faculty { get; set; } = null!;
    public string ProfilePictureUrl { get; set; } = string.Empty;

    [TempData]
    public string? StatusMessage { get; set; }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var faculty = await _context.Faculties
            .Include(f => f.User)
            .FirstOrDefaultAsync(f => f.Id == id);

        if (faculty == null)
        {
            return NotFound();
        }

        Faculty = faculty;
        ProfilePictureUrl = _fileUploadService.GetProfilePictureUrl(faculty.ProfilePicturePath);
        return Page();
    }

    public async Task<IActionResult> OnPostDeactivateAsync(int id)
    {
        var faculty = await _context.Faculties
            .FirstOrDefaultAsync(f => f.Id == id);

        if (faculty == null)
        {
            return NotFound();
        }

        if (!faculty.IsActive)
        {
            StatusMessage = "Faculty member is already inactive.";
            return RedirectToPage(new { id });
        }

        faculty.IsActive = false;
        await _context.SaveChangesAsync();

        // Audit log
        var currentUser = await _userManager.GetUserAsync(User);
        await _auditService.LogAsync(
            action: "DeactivateFaculty",
            userId: faculty.UserId,
            performedByUserId: currentUser?.Id,
            details: $"Deactivated faculty: {faculty.FullName} (ID: {faculty.EmployeeId})",
            ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString());

        StatusMessage = $"Faculty member {faculty.FullName} has been deactivated.";
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostReactivateAsync(int id)
    {
        var faculty = await _context.Faculties
            .FirstOrDefaultAsync(f => f.Id == id);

        if (faculty == null)
        {
            return NotFound();
        }

        if (faculty.IsActive)
        {
            StatusMessage = "Faculty member is already active.";
            return RedirectToPage(new { id });
        }

        faculty.IsActive = true;
        await _context.SaveChangesAsync();

        // Audit log
        var currentUser = await _userManager.GetUserAsync(User);
        await _auditService.LogAsync(
            action: "ReactivateFaculty",
            userId: faculty.UserId,
            performedByUserId: currentUser?.Id,
            details: $"Reactivated faculty: {faculty.FullName} (ID: {faculty.EmployeeId})",
            ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString());

        StatusMessage = $"Faculty member {faculty.FullName} has been reactivated.";
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostUnlinkUserAsync(int id)
    {
        var faculty = await _context.Faculties
            .Include(f => f.User)
            .FirstOrDefaultAsync(f => f.Id == id);

        if (faculty == null)
        {
            return NotFound();
        }

        if (faculty.UserId == null)
        {
            StatusMessage = "Faculty member is not linked to any user account.";
            return RedirectToPage(new { id });
        }

        var linkedUserName = faculty.User?.UserName;
        faculty.UserId = null;
        await _context.SaveChangesAsync();

        // Audit log
        var currentUser = await _userManager.GetUserAsync(User);
        await _auditService.LogAsync(
            action: "UnlinkFacultyUser",
            performedByUserId: currentUser?.Id,
            details: $"Unlinked user {linkedUserName} from faculty: {faculty.FullName} (ID: {faculty.EmployeeId})",
            ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString());

        StatusMessage = $"User account unlinked from {faculty.FullName}.";
        return RedirectToPage(new { id });
    }
}
