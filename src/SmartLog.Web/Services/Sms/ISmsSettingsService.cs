namespace SmartLog.Web.Services.Sms;

/// <summary>
/// Service for managing SMS configuration settings
/// </summary>
public interface ISmsSettingsService
{
    /// <summary>
    /// Get a setting value by key
    /// </summary>
    Task<string?> GetSettingAsync(string key);

    /// <summary>
    /// Set a setting value
    /// </summary>
    Task SetSettingAsync(string key, string? value, string category);

    /// <summary>
    /// Get all settings in a category
    /// </summary>
    Task<Dictionary<string, string?>> GetSettingsByCategoryAsync(string category);

    /// <summary>
    /// Check if SMS notifications are globally enabled
    /// </summary>
    Task<bool> IsSmsEnabledAsync();

    /// <summary>
    /// Get all settings
    /// </summary>
    Task<Dictionary<string, string?>> GetAllSettingsAsync();
}
