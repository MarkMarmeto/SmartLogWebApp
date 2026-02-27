using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SmartLog.Web.Data.Entities;
using SmartLog.Web.Services;

namespace SmartLog.Web.Pages.Admin;

[Authorize(Policy = "CanManageSettings")]
public class SettingsModel : PageModel
{
    private readonly IAppSettingsService _appSettingsService;
    private readonly UserManager<ApplicationUser> _userManager;

    public SettingsModel(
        IAppSettingsService appSettingsService,
        UserManager<ApplicationUser> userManager)
    {
        _appSettingsService = appSettingsService;
        _userManager = userManager;
    }

    public List<AppSettings> AllSettings { get; set; } = new();
    public string ActiveTab { get; set; } = "Security";

    [TempData]
    public string? StatusMessage { get; set; }

    [TempData]
    public string? ErrorMessage { get; set; }

    public List<string> Categories => new() { "Security", "QRCode", "FileUpload", "Attendance", "System" };

    public async Task OnGetAsync(string? tab)
    {
        ActiveTab = tab ?? "Security";
        AllSettings = await _appSettingsService.GetAllAsync();
    }

    public async Task<IActionResult> OnPostSaveAsync(string category, Dictionary<string, string?> settings)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        var updatedBy = currentUser?.UserName ?? "Unknown";

        // Get existing settings to know which are sensitive
        var existing = await _appSettingsService.GetAllAsync();
        var existingMap = existing.ToDictionary(s => s.Key, s => s);

        foreach (var (key, value) in settings)
        {
            if (!key.StartsWith(category + ".") && !existingMap.ContainsKey(key))
                continue;

            var isSensitive = existingMap.TryGetValue(key, out var ex) && ex.IsSensitive;
            var description = ex?.Description;

            // Skip saving sensitive fields that haven't been changed (still masked)
            if (isSensitive && value == "********")
                continue;

            await _appSettingsService.SetAsync(key, value, category, updatedBy, isSensitive, description);
        }

        StatusMessage = $"{category} settings saved successfully.";
        return RedirectToPage(new { tab = category });
    }

    public async Task<IActionResult> OnPostResetAsync(string key)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        var updatedBy = currentUser?.UserName ?? "Unknown";

        // Get the default value by checking what SeedDefaultsAsync would set
        var defaultValue = GetDefaultValue(key);
        if (defaultValue == null)
        {
            ErrorMessage = "No default value available for this setting.";
            return RedirectToPage();
        }

        var existing = (await _appSettingsService.GetAllAsync()).FirstOrDefault(s => s.Key == key);
        var category = existing?.Category ?? key.Split('.')[0];
        var isSensitive = existing?.IsSensitive ?? false;

        await _appSettingsService.SetAsync(key, defaultValue, category, updatedBy, isSensitive, existing?.Description);

        StatusMessage = $"Setting '{key}' reset to default.";
        return RedirectToPage(new { tab = category });
    }

    private static string? GetDefaultValue(string key) => key switch
    {
        "Security.PasswordMinLength" => "8",
        "Security.PasswordRequireDigit" => "true",
        "Security.PasswordRequireLowercase" => "true",
        "Security.PasswordRequireUppercase" => "true",
        "Security.PasswordRequireSpecialChar" => "false",
        "Security.LockoutDurationMinutes" => "15",
        "Security.MaxFailedAttempts" => "5",
        "Security.SessionTimeoutHours" => "10",
        "QRCode.DuplicateScanWindowMinutes" => "5",
        "FileUpload.MaxFileSizeMB" => "5",
        "FileUpload.AllowedExtensions" => ".jpg,.jpeg,.png,.gif",
        "Attendance.DefaultPageSize" => "50",
        "System.ApplicationVersion" => "1.0.0",
        "System.SchoolName" => "SmartLog School",
        "System.SchoolTimezone" => "Asia/Manila",
        _ => null
    };
}
