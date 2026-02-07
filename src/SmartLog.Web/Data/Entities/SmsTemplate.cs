using System.ComponentModel.DataAnnotations;

namespace SmartLog.Web.Data.Entities;

/// <summary>
/// SMS template for various notification types.
/// Supports bilingual templates (English and Filipino).
/// </summary>
public class SmsTemplate
{
    public int Id { get; set; }

    /// <summary>
    /// Template code: ENTRY, EXIT, HOLIDAY, SUSPENSION, EMERGENCY
    /// </summary>
    [Required]
    [StringLength(50)]
    public string Code { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// English template with placeholders like {StudentName}, {Time}, etc.
    /// </summary>
    [Required]
    [StringLength(500)]
    public string TemplateEn { get; set; } = string.Empty;

    /// <summary>
    /// Filipino template with placeholders
    /// </summary>
    [Required]
    [StringLength(500)]
    public string TemplateFil { get; set; } = string.Empty;

    /// <summary>
    /// Comma-separated list of available placeholders
    /// Example: {StudentName},{Grade},{Section},{Time},{Date}
    /// </summary>
    [StringLength(500)]
    public string? AvailablePlaceholders { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>
    /// System templates cannot be deleted, only modified
    /// </summary>
    public bool IsSystem { get; set; } = false;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
