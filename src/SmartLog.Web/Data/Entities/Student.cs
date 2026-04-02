using System.ComponentModel.DataAnnotations;

namespace SmartLog.Web.Data.Entities;

/// <summary>
/// Student entity for SmartLog.
/// Represents a student enrolled in the school.
/// </summary>
public class Student
{
    public Guid Id { get; set; }

    /// <summary>
    /// Student ID format: YYYY-GG-NNNN
    /// Example: 2026-05-0001 (First Grade 5 student enrolled in 2026)
    /// </summary>
    [Required]
    [StringLength(13)]
    public string StudentId { get; set; } = string.Empty;

    /// <summary>
    /// Learner Reference Number - 12 digits (DepEd compliance, optional)
    /// </summary>
    [StringLength(12)]
    public string? LRN { get; set; }

    [Required]
    [StringLength(100)]
    public string FirstName { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string LastName { get; set; } = string.Empty;

    [StringLength(100)]
    public string? MiddleName { get; set; }

    /// <summary>
    /// Grade level: K, 1-12
    /// </summary>
    [Required]
    [StringLength(10)]
    public string GradeLevel { get; set; } = string.Empty;

    [Required]
    [StringLength(50)]
    public string Section { get; set; } = string.Empty;

    [Required]
    [StringLength(200)]
    public string ParentGuardianName { get; set; } = string.Empty;

    /// <summary>
    /// Guardian relationship: Mother, Father, Guardian, Other
    /// </summary>
    [Required]
    [StringLength(50)]
    public string GuardianRelationship { get; set; } = string.Empty;

    [Required]
    [Phone]
    [StringLength(20)]
    public string ParentPhone { get; set; } = string.Empty;

    /// <summary>
    /// Alternate phone number for SMS notifications
    /// </summary>
    [Phone]
    [StringLength(20)]
    public string? AlternatePhone { get; set; }

    /// <summary>
    /// Enable/disable SMS notifications for this student
    /// </summary>
    public bool SmsEnabled { get; set; } = true;

    /// <summary>
    /// SMS language preference: EN (English) or FIL (Filipino)
    /// </summary>
    [Required]
    [StringLength(10)]
    public string SmsLanguage { get; set; } = "EN";

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Path to profile picture (relative to wwwroot)
    /// </summary>
    [StringLength(500)]
    public string? ProfilePicturePath { get; set; }

    /// <summary>
    /// Quick reference to the student's current enrollment.
    /// Nullable - will be null if student has no active enrollment.
    /// </summary>
    public Guid? CurrentEnrollmentId { get; set; }

    // Navigation properties
    public virtual QrCode? QrCode { get; set; }
    public virtual StudentEnrollment? CurrentEnrollment { get; set; }
    public virtual ICollection<StudentEnrollment> Enrollments { get; set; } = new List<StudentEnrollment>();

    // Computed property
    public string FullName => string.IsNullOrWhiteSpace(MiddleName)
        ? $"{FirstName} {LastName}"
        : $"{FirstName} {MiddleName} {LastName}";
}
