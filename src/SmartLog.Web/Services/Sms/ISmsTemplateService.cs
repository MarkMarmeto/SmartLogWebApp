using SmartLog.Web.Data.Entities;

namespace SmartLog.Web.Services.Sms;

/// <summary>
/// Service for managing and rendering SMS templates
/// </summary>
public interface ISmsTemplateService
{
    /// <summary>
    /// Render a template with placeholders replaced
    /// </summary>
    Task<string> RenderTemplateAsync(string code, string language, Dictionary<string, string> placeholders);

    /// <summary>
    /// Get all templates
    /// </summary>
    Task<List<SmsTemplate>> GetAllTemplatesAsync();

    /// <summary>
    /// Get template by code
    /// </summary>
    Task<SmsTemplate?> GetTemplateByCodeAsync(string code);

    /// <summary>
    /// Get template by ID
    /// </summary>
    Task<SmsTemplate?> GetTemplateByIdAsync(int id);

    /// <summary>
    /// Create new template
    /// </summary>
    Task<SmsTemplate> CreateTemplateAsync(SmsTemplate template);

    /// <summary>
    /// Update existing template
    /// </summary>
    Task<SmsTemplate> UpdateTemplateAsync(SmsTemplate template);

    /// <summary>
    /// Delete template (only non-system templates)
    /// </summary>
    Task<bool> DeleteTemplateAsync(int id);
}
