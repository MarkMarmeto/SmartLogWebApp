using System.ComponentModel.DataAnnotations;

namespace SmartLog.Web.Data.Entities;

/// <summary>
/// Represents a calendar event (holiday, school event, or class suspension).
/// Implements school calendar system for tracking holidays, events, and suspensions.
/// </summary>
public class CalendarEvent
{
    public Guid Id { get; set; }

    [Required]
    [StringLength(200)]
    public string Title { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Description { get; set; }

    [Required]
    public EventType EventType { get; set; }

    [Required]
    [StringLength(50)]
    public string Category { get; set; } = string.Empty;

    // Date/Time
    [Required]
    public DateTime StartDate { get; set; }

    [Required]
    public DateTime EndDate { get; set; }

    public bool IsAllDay { get; set; } = true;

    public TimeSpan? StartTime { get; set; }

    public TimeSpan? EndTime { get; set; }

    // Impact
    public bool AffectsAttendance { get; set; }

    public bool AffectsClasses { get; set; }

    // Scope - JSON array of grade codes, e.g., ["7","8","9"]
    // NULL means all grades
    [StringLength(100)]
    public string? AffectedGrades { get; set; }

    // When EventType == Event: true suppresses the No-Scan Alert for AffectedGrades (null = all grades).
    // Holiday and Suspension always suppress regardless of this flag.
    public bool? SuppressesNoScanAlert { get; set; }

    [StringLength(200)]
    public string? Location { get; set; }

    // Recurrence
    public bool IsRecurring { get; set; }

    [StringLength(100)]
    public string? RecurrencePattern { get; set; }

    public DateTime? RecurrenceEndDate { get; set; }

    // Organization
    [Required]
    public Guid AcademicYearId { get; set; }

    public string? OrganizerId { get; set; }

    // Metadata
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Required]
    [StringLength(450)]
    public string CreatedBy { get; set; } = string.Empty;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [StringLength(450)]
    public string? UpdatedBy { get; set; }

    public bool IsActive { get; set; } = true;

    // Navigation properties
    public AcademicYear AcademicYear { get; set; } = null!;
    public ApplicationUser? Organizer { get; set; }
}

/// <summary>
/// Types of calendar events.
/// </summary>
public enum EventType
{
    Holiday,
    Event,
    Suspension
}
