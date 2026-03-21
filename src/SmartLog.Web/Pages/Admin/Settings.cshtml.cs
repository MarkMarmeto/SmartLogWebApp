using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SmartLog.Web.Data;
using SmartLog.Web.Data.Entities;
using SmartLog.Web.Services;

namespace SmartLog.Web.Pages.Admin;

[Authorize(Policy = "CanManageSettings")]
public class SettingsModel : PageModel
{
    private readonly IAppSettingsService _appSettingsService;
    private readonly ApplicationDbContext _context;
    private readonly IAuditService _auditService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<SettingsModel> _logger;

    public SettingsModel(
        IAppSettingsService appSettingsService,
        ApplicationDbContext context,
        IAuditService auditService,
        UserManager<ApplicationUser> userManager,
        ILogger<SettingsModel> logger)
    {
        _appSettingsService = appSettingsService;
        _context = context;
        _auditService = auditService;
        _userManager = userManager;
        _logger = logger;
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

        var hmacKeyChanged = false;

        foreach (var (key, value) in settings)
        {
            if (!key.StartsWith(category + ".") && !existingMap.ContainsKey(key))
                continue;

            var isSensitive = existingMap.TryGetValue(key, out var ex) && ex.IsSensitive;
            var description = ex?.Description;

            // Skip saving sensitive fields that haven't been changed (still masked)
            if (isSensitive && value == "********")
                continue;

            // Detect HMAC key change
            if (key == "QRCode.HmacSecretKey" && value != ex?.Value)
                hmacKeyChanged = true;

            await _appSettingsService.SetAsync(key, value, category, updatedBy, isSensitive, description);
        }

        // When HMAC key changes, invalidate all existing QR codes
        if (hmacKeyChanged)
        {
            var invalidatedCount = await InvalidateAllQrCodesAsync(updatedBy);
            StatusMessage = $"{category} settings saved. {invalidatedCount} existing QR code(s) invalidated — students will need new QR codes.";
            _logger.LogWarning("HMAC secret key changed by {User}. {Count} QR codes invalidated.", updatedBy, invalidatedCount);
        }
        else
        {
            StatusMessage = $"{category} settings saved successfully.";
        }

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

    private async Task<int> InvalidateAllQrCodesAsync(string updatedBy)
    {
        var validQrCodes = await _context.QrCodes
            .Where(q => q.IsValid)
            .ToListAsync();

        foreach (var qr in validQrCodes)
        {
            qr.IsValid = false;
        }

        await _context.SaveChangesAsync();

        await _auditService.LogAsync(
            action: "AllQrCodesInvalidated",
            performedByUserId: null,
            details: $"All {validQrCodes.Count} QR codes invalidated due to HMAC secret key change by {updatedBy}");

        return validQrCodes.Count;
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
        "Attendance.EnforceSchoolDayValidation" => "true",
        "System.ApplicationVersion" => "1.0.0",
        "System.SchoolName" => "SmartLog School",
        "System.SchoolTimezone" => "Asia/Manila",
        _ => null
    };
}
