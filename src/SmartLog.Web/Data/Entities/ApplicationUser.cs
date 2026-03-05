using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace SmartLog.Web.Data.Entities;

/// <summary>
/// Extended user entity for SmartLog application.
/// Adds custom fields to ASP.NET Identity user.
/// </summary>
public class ApplicationUser : IdentityUser
{
    [Required]
    [StringLength(100)]
    public string FirstName { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string LastName { get; set; } = string.Empty;

    /// <summary>
    /// Soft delete flag. Inactive users cannot login.
    /// </summary>
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When true, user must change password before accessing any page.
    /// Set on account creation and admin password reset.
    /// </summary>
    public bool MustChangePassword { get; set; } = false;

    /// <summary>
    /// Path to profile picture (relative to wwwroot)
    /// </summary>
    [StringLength(500)]
    public string? ProfilePicturePath { get; set; }

    /// <summary>
    /// Full name for display purposes.
    /// </summary>
    public string FullName => $"{FirstName} {LastName}";
}
