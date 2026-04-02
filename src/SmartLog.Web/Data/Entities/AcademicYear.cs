using System.ComponentModel.DataAnnotations;

namespace SmartLog.Web.Data.Entities;

/// <summary>
/// Academic year entity for SmartLog.
/// Represents a school year period.
/// </summary>
public class AcademicYear
{
    public Guid Id { get; set; }

    /// <summary>
    /// Academic year name: 2025-2026, 2026-2027, etc.
    /// </summary>
    [Required]
    [StringLength(20)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Start date of the academic year
    /// </summary>
    public DateTime StartDate { get; set; }

    /// <summary>
    /// End date of the academic year
    /// </summary>
    public DateTime EndDate { get; set; }

    /// <summary>
    /// Indicates if this is the current active academic year.
    /// Only one academic year should have IsCurrent = true at a time.
    /// </summary>
    public bool IsCurrent { get; set; } = false;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public virtual ICollection<StudentEnrollment> Enrollments { get; set; } = new List<StudentEnrollment>();
    public virtual ICollection<Scan> Scans { get; set; } = new List<Scan>();
}
