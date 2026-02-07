namespace SmartLog.Web.Data.Entities;

/// <summary>
/// Student enrollment entity for SmartLog.
/// Tracks a student's enrollment in a section for a specific academic year.
/// </summary>
public class StudentEnrollment
{
    public int Id { get; set; }

    /// <summary>
    /// Foreign key to Student
    /// </summary>
    public int StudentId { get; set; }

    /// <summary>
    /// Foreign key to Section
    /// </summary>
    public int SectionId { get; set; }

    /// <summary>
    /// Foreign key to AcademicYear
    /// </summary>
    public int AcademicYearId { get; set; }

    /// <summary>
    /// Date when the student was enrolled in this section
    /// </summary>
    public DateTime EnrolledAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Indicates if this enrollment is currently active.
    /// Set to false for withdrawals or transfers within the same academic year.
    /// </summary>
    public bool IsActive { get; set; } = true;

    // Navigation properties
    public virtual Student Student { get; set; } = null!;
    public virtual Section Section { get; set; } = null!;
    public virtual AcademicYear AcademicYear { get; set; } = null!;
}
