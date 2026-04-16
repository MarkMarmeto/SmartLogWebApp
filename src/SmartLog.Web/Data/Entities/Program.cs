using System.ComponentModel.DataAnnotations;

namespace SmartLog.Web.Data.Entities;

public class Program
{
    public Guid Id { get; set; }

    [Required]
    [StringLength(20)]
    public string Code { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;

    public int SortOrder { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public virtual ICollection<GradeLevelProgram> GradeLevelPrograms { get; set; } = new List<GradeLevelProgram>();
    public virtual ICollection<Section> Sections { get; set; } = new List<Section>();
}
