using System.ComponentModel.DataAnnotations;

namespace SmartLog.Web.Data.Entities;

/// <summary>
/// Grade level entity for SmartLog.
/// Represents a grade level in the school (K, 1-12).
/// </summary>
public class GradeLevel
{
    public int Id { get; set; }

    /// <summary>
    /// Grade code: K, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12
    /// </summary>
    [Required]
    [StringLength(10)]
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Display name: Grade 7, Grade 8, etc.
    /// </summary>
    [Required]
    [StringLength(50)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Sort order for display (K=0, 1=1, 2=2, etc.)
    /// </summary>
    public int SortOrder { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public virtual ICollection<Section> Sections { get; set; } = new List<Section>();
}
