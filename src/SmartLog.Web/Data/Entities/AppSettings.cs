using System.ComponentModel.DataAnnotations;

namespace SmartLog.Web.Data.Entities;

public class AppSettings
{
    public int Id { get; set; }

    [Required]
    [StringLength(100)]
    public string Key { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Value { get; set; }

    [Required]
    [StringLength(50)]
    public string Category { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Description { get; set; }

    public bool IsSensitive { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [StringLength(256)]
    public string? UpdatedBy { get; set; }
}
