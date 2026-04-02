using System.ComponentModel.DataAnnotations;

namespace SmartLog.Web.Data.Entities;

/// <summary>
/// Dynamic SMS configuration stored in database
/// </summary>
public class SmsSettings
{
    public Guid Id { get; set; }

    [Required]
    [StringLength(100)]
    public string Key { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Value { get; set; }

    /// <summary>
    /// Category: General, GSM, Cloud
    /// </summary>
    [Required]
    [StringLength(50)]
    public string Category { get; set; } = string.Empty;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
