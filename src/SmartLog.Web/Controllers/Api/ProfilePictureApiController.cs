using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartLog.Web.Data;
using SmartLog.Web.Data.Entities;
using SmartLog.Web.Services;

namespace SmartLog.Web.Controllers.Api;

/// <summary>
/// API controller for profile picture uploads.
/// </summary>
[ApiController]
[Route("api/v1/profile-picture")]
[Authorize]
public class ProfilePictureApiController : ControllerBase
{
    private readonly IFileUploadService _fileUploadService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ApplicationDbContext _context;
    private readonly IAuditService _auditService;
    private readonly ILogger<ProfilePictureApiController> _logger;

    public ProfilePictureApiController(
        IFileUploadService fileUploadService,
        UserManager<ApplicationUser> userManager,
        ApplicationDbContext context,
        IAuditService auditService,
        ILogger<ProfilePictureApiController> logger)
    {
        _fileUploadService = fileUploadService;
        _userManager = userManager;
        _context = context;
        _auditService = auditService;
        _logger = logger;
    }

    /// <summary>
    /// Upload profile picture for current user.
    /// </summary>
    [HttpPost("user")]
    public async Task<IActionResult> UploadUserProfilePicture(IFormFile file)
    {
        try
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Unauthorized();
            }

            if (!await _fileUploadService.IsValidImageAsync(file))
            {
                return BadRequest(new { error = "Invalid image file. Please upload a JPG, PNG, or GIF file under 5MB." });
            }

            // Delete old picture if exists
            await _fileUploadService.DeleteProfilePictureAsync(user.ProfilePicturePath);

            // Upload new picture
            var filePath = await _fileUploadService.UploadProfilePictureAsync(file, "users", user.Id);

            // Update user
            user.ProfilePicturePath = filePath;
            user.UpdatedAt = DateTime.UtcNow;
            await _userManager.UpdateAsync(user);

            await _auditService.LogAsync(
                action: "ProfilePictureUpdated",
                userId: user.Id,
                performedByUserId: user.Id,
                details: $"User {user.UserName} updated their profile picture",
                ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString(),
                userAgent: Request.Headers.UserAgent.ToString());

            return Ok(new { path = filePath, url = _fileUploadService.GetProfilePictureUrl(filePath) });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading user profile picture");
            return StatusCode(500, new { error = "Failed to upload profile picture" });
        }
    }

    /// <summary>
    /// Upload profile picture for a student.
    /// </summary>
    [HttpPost("student/{id}")]
    [Authorize(Policy = "CanManageStudents")]
    public async Task<IActionResult> UploadStudentProfilePicture(Guid id, IFormFile file)
    {
        try
        {
            var student = await _context.Students.FindAsync(id);
            if (student == null)
            {
                return NotFound(new { error = "Student not found" });
            }

            if (!await _fileUploadService.IsValidImageAsync(file))
            {
                return BadRequest(new { error = "Invalid image file. Please upload a JPG, PNG, or GIF file under 5MB." });
            }

            // Delete old picture if exists
            await _fileUploadService.DeleteProfilePictureAsync(student.ProfilePicturePath);

            // Upload new picture
            var filePath = await _fileUploadService.UploadProfilePictureAsync(file, "students", id.ToString());

            // Update student
            student.ProfilePicturePath = filePath;
            student.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            await _auditService.LogAsync(
                action: "ProfilePictureUpdated",
                userId: null,
                performedByUserId: _userManager.GetUserId(User),
                details: $"Profile picture updated for student {student.FullName} ({student.StudentId})",
                ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString(),
                userAgent: Request.Headers.UserAgent.ToString());

            return Ok(new { path = filePath, url = _fileUploadService.GetProfilePictureUrl(filePath) });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading student profile picture for ID {StudentId}", id);
            return StatusCode(500, new { error = "Failed to upload profile picture" });
        }
    }

    /// <summary>
    /// Upload profile picture for faculty.
    /// </summary>
    [HttpPost("faculty/{id}")]
    [Authorize(Policy = "CanManageFaculty")]
    public async Task<IActionResult> UploadFacultyProfilePicture(Guid id, IFormFile file)
    {
        try
        {
            var faculty = await _context.Faculties.FindAsync(id);
            if (faculty == null)
            {
                return NotFound(new { error = "Faculty member not found" });
            }

            if (!await _fileUploadService.IsValidImageAsync(file))
            {
                return BadRequest(new { error = "Invalid image file. Please upload a JPG, PNG, or GIF file under 5MB." });
            }

            // Delete old picture if exists
            await _fileUploadService.DeleteProfilePictureAsync(faculty.ProfilePicturePath);

            // Upload new picture
            var filePath = await _fileUploadService.UploadProfilePictureAsync(file, "faculty", id.ToString());

            // Update faculty
            faculty.ProfilePicturePath = filePath;
            faculty.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            await _auditService.LogAsync(
                action: "ProfilePictureUpdated",
                userId: null,
                performedByUserId: _userManager.GetUserId(User),
                details: $"Profile picture updated for faculty {faculty.FullName} ({faculty.EmployeeId})",
                ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString(),
                userAgent: Request.Headers.UserAgent.ToString());

            return Ok(new { path = filePath, url = _fileUploadService.GetProfilePictureUrl(filePath) });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading faculty profile picture for ID {FacultyId}", id);
            return StatusCode(500, new { error = "Failed to upload profile picture" });
        }
    }

    /// <summary>
    /// Delete profile picture for current user.
    /// </summary>
    [HttpDelete("user")]
    public async Task<IActionResult> DeleteUserProfilePicture()
    {
        try
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Unauthorized();
            }

            await _fileUploadService.DeleteProfilePictureAsync(user.ProfilePicturePath);

            user.ProfilePicturePath = null;
            user.UpdatedAt = DateTime.UtcNow;
            await _userManager.UpdateAsync(user);

            await _auditService.LogAsync(
                action: "ProfilePictureDeleted",
                userId: user.Id,
                performedByUserId: user.Id,
                details: $"User {user.UserName} deleted their profile picture",
                ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString(),
                userAgent: Request.Headers.UserAgent.ToString());

            return Ok(new { message = "Profile picture deleted successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting user profile picture");
            return StatusCode(500, new { error = "Failed to delete profile picture" });
        }
    }

    /// <summary>
    /// Delete profile picture for a student.
    /// </summary>
    [HttpDelete("student/{id}")]
    [Authorize(Policy = "CanManageStudents")]
    public async Task<IActionResult> DeleteStudentProfilePicture(Guid id)
    {
        try
        {
            var student = await _context.Students.FindAsync(id);
            if (student == null)
            {
                return NotFound(new { error = "Student not found" });
            }

            await _fileUploadService.DeleteProfilePictureAsync(student.ProfilePicturePath);

            student.ProfilePicturePath = null;
            student.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            await _auditService.LogAsync(
                action: "ProfilePictureDeleted",
                userId: null,
                performedByUserId: _userManager.GetUserId(User),
                details: $"Profile picture deleted for student {student.FullName} ({student.StudentId})",
                ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString(),
                userAgent: Request.Headers.UserAgent.ToString());

            return Ok(new { message = "Profile picture deleted successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting student profile picture for ID {StudentId}", id);
            return StatusCode(500, new { error = "Failed to delete profile picture" });
        }
    }

    /// <summary>
    /// Delete profile picture for faculty.
    /// </summary>
    [HttpDelete("faculty/{id}")]
    [Authorize(Policy = "CanManageFaculty")]
    public async Task<IActionResult> DeleteFacultyProfilePicture(Guid id)
    {
        try
        {
            var faculty = await _context.Faculties.FindAsync(id);
            if (faculty == null)
            {
                return NotFound(new { error = "Faculty member not found" });
            }

            await _fileUploadService.DeleteProfilePictureAsync(faculty.ProfilePicturePath);

            faculty.ProfilePicturePath = null;
            faculty.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            await _auditService.LogAsync(
                action: "ProfilePictureDeleted",
                userId: null,
                performedByUserId: _userManager.GetUserId(User),
                details: $"Profile picture deleted for faculty {faculty.FullName} ({faculty.EmployeeId})",
                ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString(),
                userAgent: Request.Headers.UserAgent.ToString());

            return Ok(new { message = "Profile picture deleted successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting faculty profile picture for ID {FacultyId}", id);
            return StatusCode(500, new { error = "Failed to delete profile picture" });
        }
    }
}
