using Microsoft.EntityFrameworkCore;
using SmartLog.Web.Data;
using SmartLog.Web.Data.Entities;

namespace SmartLog.Web.Services.Sms;

/// <summary>
/// Service for managing and rendering SMS templates
/// </summary>
public class SmsTemplateService : ISmsTemplateService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<SmsTemplateService> _logger;

    public SmsTemplateService(
        ApplicationDbContext context,
        ILogger<SmsTemplateService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<string> RenderTemplateAsync(
        string code,
        string language,
        Dictionary<string, string> placeholders)
    {
        try
        {
            var template = await GetTemplateByCodeAsync(code);
            if (template == null)
            {
                _logger.LogWarning("Template not found: {Code}", code);
                return string.Empty;
            }

            // Select template based on language
            var templateText = language.ToUpperInvariant() == "FIL"
                ? template.TemplateFil
                : template.TemplateEn;

            // Replace placeholders
            foreach (var placeholder in placeholders)
            {
                var key = $"{{{placeholder.Key}}}";
                templateText = templateText.Replace(key, placeholder.Value);
            }

            return templateText;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rendering template: {Code}", code);
            return string.Empty;
        }
    }

    public async Task<List<SmsTemplate>> GetAllTemplatesAsync()
    {
        try
        {
            return await _context.SmsTemplates
                .OrderBy(t => t.Code)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting all templates");
            return new List<SmsTemplate>();
        }
    }

    public async Task<SmsTemplate?> GetTemplateByCodeAsync(string code)
    {
        try
        {
            return await _context.SmsTemplates
                .FirstOrDefaultAsync(t => t.Code == code);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting template by code: {Code}", code);
            return null;
        }
    }

    public async Task<SmsTemplate?> GetTemplateByIdAsync(Guid id)
    {
        try
        {
            return await _context.SmsTemplates
                .FirstOrDefaultAsync(t => t.Id == id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting template by ID: {Id}", id);
            return null;
        }
    }

    public async Task<SmsTemplate> CreateTemplateAsync(SmsTemplate template)
    {
        try
        {
            template.CreatedAt = DateTime.UtcNow;
            template.UpdatedAt = DateTime.UtcNow;

            _context.SmsTemplates.Add(template);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Created SMS template: {Code}", template.Code);
            return template;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating template: {Code}", template.Code);
            throw;
        }
    }

    public async Task<SmsTemplate> UpdateTemplateAsync(SmsTemplate template)
    {
        try
        {
            var existing = await GetTemplateByIdAsync(template.Id);
            if (existing == null)
            {
                throw new InvalidOperationException($"Template not found: {template.Id}");
            }

            existing.Name = template.Name;
            existing.TemplateEn = template.TemplateEn;
            existing.TemplateFil = template.TemplateFil;
            existing.AvailablePlaceholders = template.AvailablePlaceholders;
            existing.IsActive = template.IsActive;
            existing.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Updated SMS template: {Code}", template.Code);
            return existing;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating template: {Id}", template.Id);
            throw;
        }
    }

    public async Task<bool> DeleteTemplateAsync(Guid id)
    {
        try
        {
            var template = await GetTemplateByIdAsync(id);
            if (template == null)
            {
                return false;
            }

            if (template.IsSystem)
            {
                _logger.LogWarning("Attempted to delete system template: {Code}", template.Code);
                return false;
            }

            _context.SmsTemplates.Remove(template);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Deleted SMS template: {Code}", template.Code);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting template: {Id}", id);
            return false;
        }
    }
}
