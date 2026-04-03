using Microsoft.EntityFrameworkCore;
using SmartLog.Web.Data;
using SmartLog.Web.Data.Entities;

namespace SmartLog.Web.Services.Sms;

/// <summary>
/// Service for managing SMS configuration settings
/// </summary>
public class SmsSettingsService : ISmsSettingsService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<SmsSettingsService> _logger;

    public SmsSettingsService(
        ApplicationDbContext context,
        ILogger<SmsSettingsService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<string?> GetSettingAsync(string key)
    {
        try
        {
            var setting = await _context.SmsSettings
                .FirstOrDefaultAsync(s => s.Key == key);

            return setting?.Value;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting SMS setting: {Key}", key);
            return null;
        }
    }

    public async Task SetSettingAsync(string key, string? value, string category)
    {
        try
        {
            var setting = await _context.SmsSettings
                .FirstOrDefaultAsync(s => s.Key == key);

            if (setting == null)
            {
                setting = new SmsSettings
                {
                    Key = key,
                    Value = value,
                    Category = category,
                    UpdatedAt = DateTime.UtcNow
                };
                _context.SmsSettings.Add(setting);
            }
            else
            {
                setting.Value = value;
                setting.Category = category;
                setting.UpdatedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();

            var isSensitive = key.Contains("ApiKey", StringComparison.OrdinalIgnoreCase)
                || key.Contains("Secret", StringComparison.OrdinalIgnoreCase)
                || key.Contains("Password", StringComparison.OrdinalIgnoreCase);
            var logValue = isSensitive ? "***" : value;
            _logger.LogInformation("SMS setting updated: {Key} = {Value}", key, logValue);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting SMS setting: {Key}", key);
            throw;
        }
    }

    public async Task<Dictionary<string, string?>> GetSettingsByCategoryAsync(string category)
    {
        try
        {
            var settings = await _context.SmsSettings
                .Where(s => s.Category == category)
                .ToDictionaryAsync(s => s.Key, s => s.Value);

            return settings;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting SMS settings by category: {Category}", category);
            return new Dictionary<string, string?>();
        }
    }

    public async Task<bool> IsSmsEnabledAsync()
    {
        try
        {
            var enabled = await GetSettingAsync("Sms.Enabled");
            return enabled != null && (enabled.Equals("true", StringComparison.OrdinalIgnoreCase) || enabled == "1");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if SMS is enabled");
            return false;
        }
    }

    public async Task<Dictionary<string, string?>> GetAllSettingsAsync()
    {
        try
        {
            var settings = await _context.SmsSettings
                .OrderBy(s => s.Category)
                .ThenBy(s => s.Key)
                .ToDictionaryAsync(s => s.Key, s => s.Value);

            return settings;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting all SMS settings");
            return new Dictionary<string, string?>();
        }
    }
}
