using System.ComponentModel.DataAnnotations;

namespace SmartLog.Web.Data.Entities;

/// <summary>
/// Faculty entity for SmartLog.
/// Represents a teacher or staff member at the school.
/// </summary>
public class Faculty
{
    public Guid Id { get; set; }

    [Required]
    [StringLength(20)]
    public string EmployeeId { get; set; } = string.Empty;

    /// <summary>
    /// Optional external employee ID for faculty who had IDs before the auto-generation system.
    /// Example: Old employee numbers, government IDs, etc.
    /// </summary>
    [StringLength(50)]
    public string? ExternalEmployeeId { get; set; }

    [Required]
    [StringLength(100)]
    public string FirstName { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
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
    public string? PhoneNumber { get; set; }

    public DateTime? HireDate { get; set; }

    /// <summary>
    /// Optional link to user account for system access.
    /// </summary>
    public string? UserId { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Path to profile picture (relative to wwwroot)
    /// </summary>
    [StringLength(500)]
    public string? ProfilePicturePath { get; set; }

    // Navigation properties
    public virtual ApplicationUser? User { get; set; }
    public virtual ICollection<Section> AdvisedSections { get; set; } = new List<Section>();

    // Computed property
    public string FullName => $"{FirstName} {LastName}";
}
