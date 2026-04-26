using System.ComponentModel.DataAnnotations;

namespace SmartLog.Web.Data.Entities;

/// <summary>
/// Section entity for SmartLog.
/// Represents a class section within a grade level.
/// </summary>
public class Section
{
    public Guid Id { get; set; }

    /// <summary>
    /// Section name: A, B, Sampaguita, etc.
    /// </summary>
    [Required]
    [StringLength(50)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Foreign key to GradeLevel
    /// </summary>
    public Guid GradeLevelId { get; set; }

    /// <summary>
    /// Foreign key to Faculty (adviser), nullable
    /// </summary>
    public Guid? AdviserId { get; set; }

    /// <summary>
    /// Maximum number of students in this section
    /// </summary>
    public int Capacity { get; set; } = 40;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// FK to Program. Required for graded grade levels; null for Non-Graded sections (US0103).
    /// </summary>
    public Guid? ProgramId { get; set; }

    // Navigation properties
    public virtual GradeLevel GradeLevel { get; set; } = null!;
    public virtual Faculty? Adviser { get; set; }
    public virtual Program? Program { get; set; }
    public virtual ICollection<StudentEnrollment> Enrollments { get; set; } = new List<StudentEnrollment>();
}
