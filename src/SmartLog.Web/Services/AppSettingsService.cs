using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using SmartLog.Web.Data;
using SmartLog.Web.Data.Entities;

namespace SmartLog.Web.Services;

public class AppSettingsService : IAppSettingsService
{
    private readonly ApplicationDbContext _context;
    private readonly IMemoryCache _cache;
    private readonly IAuditService _auditService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AppSettingsService> _logger;
    private const string CachePrefix = "AppSetting_";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(60);

    public AppSettingsService(
        ApplicationDbContext context,
        IMemoryCache cache,
        IAuditService auditService,
        IConfiguration configuration,
        ILogger<AppSettingsService> logger)
    {
        _context = context;
        _cache = cache;
        _auditService = auditService;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<string?> GetAsync(string key)
    {
        var cacheKey = CachePrefix + key;
        if (_cache.TryGetValue(cacheKey, out string? cached))
        {
            return cached;
        }

        try
        {
            var setting = await _context.AppSettings
                .FirstOrDefaultAsync(s => s.Key == key);

            var value = setting?.Value;
            _cache.Set(cacheKey, value, CacheDuration);
            return value;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting app setting: {Key}", key);
            return null;
        }
    }

    public async Task<T> GetAsync<T>(string key, T defaultValue)
    {
        var value = await GetAsync(key);
        if (string.IsNullOrEmpty(value))
        {
            return defaultValue;
        }

        try
        {
            var targetType = typeof(T);
            if (targetType == typeof(int))
                return (T)(object)int.Parse(value);
            if (targetType == typeof(bool))
                return (T)(object)(value.Equals("true", StringComparison.OrdinalIgnoreCase) || value == "1");
            if (targetType == typeof(double))
                return (T)(object)double.Parse(value);
            if (targetType == typeof(long))
                return (T)(object)long.Parse(value);
            if (targetType == typeof(string))
                return (T)(object)value;

            return defaultValue;
        }
        catch
        {
            return defaultValue;
        }
    }

    public async Task SetAsync(string key, string? value, string category, string? updatedBy, bool isSensitive = false, string? description = null)
    {
        try
        {
            var setting = await _context.AppSettings
                .FirstOrDefaultAsync(s => s.Key == key);

            if (setting == null)
            {
                setting = new AppSettings
                {
                    Key = key,
                    Value = value,
                    Category = category,
                    IsSensitive = isSensitive,
                    Description = description,
                    UpdatedAt = DateTime.UtcNow,
                    UpdatedBy = updatedBy
                };
                _context.AppSettings.Add(setting);
            }
            else
            {
                setting.Value = value;
                setting.Category = category;
                setting.IsSensitive = isSensitive;
                if (description != null)
                    setting.Description = description;
                setting.UpdatedAt = DateTime.UtcNow;
                setting.UpdatedBy = updatedBy;
            }

            await _context.SaveChangesAsync();

            // Invalidate cache
            _cache.Remove(CachePrefix + key);

            // Audit log - mask sensitive values
            var logValue = isSensitive ? "***" : value;
            _logger.LogInformation("App setting updated: {Key} by {UpdatedBy}", key, updatedBy);

            await _auditService.LogAsync(
                action: "SettingChanged",
                performedByUserId: null,
                details: $"Setting '{key}' changed to '{logValue}' by {updatedBy}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting app setting: {Key}", key);
            throw;
        }
    }

    public async Task<Dictionary<string, string?>> GetByCategoryAsync(string category)
    {
        try
        {
            return await _context.AppSettings
                .Where(s => s.Category == category)
                .ToDictionaryAsync(s => s.Key, s => s.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting app settings by category: {Category}", category);
            return new Dictionary<string, string?>();
        }
    }

    public async Task<List<AppSettings>> GetAllAsync()
    {
        try
        {
            return await _context.AppSettings
                .OrderBy(s => s.Category)
                .ThenBy(s => s.Key)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting all app settings");
            return new List<AppSettings>();
        }
    }

    public async Task SeedDefaultsAsync()
    {
        var defaults = new List<(string Key, string Value, string Category, bool IsSensitive, string Description)>
        {
            ("Security.PasswordMinLength", "8", "Security", false, "Minimum password length"),
            ("Security.PasswordRequireDigit", "true", "Security", false, "Require digit in password"),
            ("Security.PasswordRequireLowercase", "true", "Security", false, "Require lowercase letter"),
            ("Security.PasswordRequireUppercase", "true", "Security", false, "Require uppercase letter"),
            ("Security.PasswordRequireSpecialChar", "false", "Security", false, "Require special character"),
            ("Security.LockoutDurationMinutes", "15", "Security", false, "Account lockout duration in minutes"),
            ("Security.MaxFailedAttempts", "5", "Security", false, "Max failed login attempts before lockout"),
            ("Security.SessionTimeoutHours", "10", "Security", false, "Session expiry time in hours"),
            ("QRCode.HmacSecretKey", GetResolvedHmacKey(), "QRCode", true, "HMAC key for QR code signing"),
            ("QRCode.DuplicateScanWindowMinutes", "5", "QRCode", false, "Duplicate scan detection window in minutes"),
            ("FileUpload.MaxFileSizeMB", "5", "FileUpload", false, "Maximum upload file size in MB"),
            ("FileUpload.AllowedExtensions", ".jpg,.jpeg,.png,.gif", "FileUpload", false, "Allowed file upload extensions"),
            ("Attendance.DefaultPageSize", "50", "Attendance", false, "Default records per page"),
            ("Attendance.EnforceSchoolDayValidation", "true", "Attendance", false, "Reject scans on non-school days"),
            ("System.ApplicationVersion", "1.0.0", "System", false, "Application version"),
            ("System.SchoolName", "SmartLog School", "System", false, "School name for display"),
            ("System.SchoolCode", "SL", "System", false, "School code used in Student ID generation (e.g., MNHS, SJA)"),
            ("System.SchoolTimezone", "Asia/Manila", "System", false, "Default timezone"),
        };

        foreach (var (key, value, category, isSensitive, description) in defaults)
        {
            var existing = await _context.AppSettings.FirstOrDefaultAsync(s => s.Key == key);
            if (existing == null)
            {
                _context.AppSettings.Add(new AppSettings
                {
                    Key = key,
                    Value = value,
                    Category = category,
                    IsSensitive = isSensitive,
                    Description = description,
                    UpdatedAt = DateTime.UtcNow,
                    UpdatedBy = "System"
                });
            }
            else if (existing.Value != null && existing.Value.StartsWith("${"))
            {
                // Fix previously seeded unresolved placeholder values
                existing.Value = value;
                existing.UpdatedAt = DateTime.UtcNow;
                existing.UpdatedBy = "System";
                _logger.LogWarning("Fixed unresolved placeholder for app setting: {Key}", key);
            }
        }

        await _context.SaveChangesAsync();
        _logger.LogInformation("App settings defaults seeded successfully");
    }

    private string GetResolvedHmacKey()
    {
        // Priority: environment variable > appsettings.json (skip unresolved placeholders)
        var envKey = Environment.GetEnvironmentVariable("SMARTLOG_HMAC_SECRET_KEY");
        if (!string.IsNullOrEmpty(envKey))
            return envKey;

        var configKey = _configuration["QrCode:HmacSecretKey"];
        if (!string.IsNullOrEmpty(configKey) && !configKey.StartsWith("${"))
            return configKey;

        return "";
    }
}
