using SmartLog.Web.Data.Entities;

namespace SmartLog.Web.Services;

public interface IAppSettingsService
{
    Task<string?> GetAsync(string key);
    Task<T> GetAsync<T>(string key, T defaultValue);
    Task SetAsync(string key, string? value, string category, string? updatedBy, bool isSensitive = false, string? description = null);
    Task<Dictionary<string, string?>> GetByCategoryAsync(string category);
    Task<List<AppSettings>> GetAllAsync();
    Task SeedDefaultsAsync();
}
