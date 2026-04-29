using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SmartLog.Web.Data;
using SmartLog.Web.Data.Entities;
using SmartLog.Web.Services;
using SmartLog.Web.Services.Branding;

namespace SmartLog.Web.Pages.Admin;

[Authorize(Policy = "CanManageSettings")]
public class SettingsModel : PageModel
{
    private readonly IAppSettingsService _appSettingsService;
    private readonly ApplicationDbContext _context;
    private readonly IAuditService _auditService;
    private readonly IBrandingService _branding;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<SettingsModel> _logger;

    public SettingsModel(
        IAppSettingsService appSettingsService,
        ApplicationDbContext context,
        IAuditService auditService,
        IBrandingService branding,
        UserManager<ApplicationUser> userManager,
        ILogger<SettingsModel> logger)
    {
        _appSettingsService = appSettingsService;
        _context = context;
        _auditService = auditService;
        _branding = branding;
        _userManager = userManager;
        _logger = logger;
    }

    public List<AppSettings> AllSettings { get; set; } = new();
    public string ActiveTab { get; set; } = "Security";

    [TempData]
    public string? StatusMessage { get; set; }

    [TempData]
    public string? ErrorMessage { get; set; }

    [BindProperty]
    public BrandingTabForm BrandingForm { get; set; } = new();

    public string? CurrentLogoPath { get; set; }

    public List<string> Categories => new() { "Security", "QRCode", "FileUpload", "Attendance", "System" };

    public async Task OnGetAsync(string? tab)
    {
        ActiveTab = tab ?? "Security";
        AllSettings = await _appSettingsService.GetAllAsync();

        if (ActiveTab == "System")
        {
            BrandingForm.SchoolAddress = await _appSettingsService.GetAsync("Branding:SchoolAddress") ?? "";
            BrandingForm.ReturnAddressText = await _appSettingsService.GetAsync("Branding:ReturnAddressText") ?? "";
            CurrentLogoPath = await _appSettingsService.GetAsync("Branding:SchoolLogoPath");
        }
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

    public async Task<IActionResult> OnPostSaveBrandingAsync()
    {
        var currentUserId = _userManager.GetUserId(User);
        var currentUserName = User.Identity?.Name;
        var changedKeys = new List<string>();

        var currentAddress = await _appSettingsService.GetAsync("Branding:SchoolAddress") ?? "";
        var newAddress = BrandingForm.SchoolAddress ?? "";
        if (currentAddress != newAddress)
        {
            await _appSettingsService.SetAsync("Branding:SchoolAddress", newAddress, "Branding", currentUserName);
            changedKeys.Add("Branding:SchoolAddress");
        }

        var currentReturn = await _appSettingsService.GetAsync("Branding:ReturnAddressText") ?? "";
        var newReturn = BrandingForm.ReturnAddressText ?? "";
        if (currentReturn != newReturn)
        {
            await _appSettingsService.SetAsync("Branding:ReturnAddressText", newReturn, "Branding", currentUserName);
            changedKeys.Add("Branding:ReturnAddressText");
        }

        if (changedKeys.Count > 0)
            await _auditService.LogAsync("SchoolBrandingUpdated", null, currentUserId,
                details: $"keys: {string.Join(", ", changedKeys)} by {currentUserName}");

        StatusMessage = "School branding saved.";
        return RedirectToPage(new { tab = "System" });
    }

    public async Task<IActionResult> OnPostUploadLogoAsync(IFormFile? logoFile)
    {
        if (logoFile == null || logoFile.Length == 0)
        {
            ErrorMessage = "Please select a file to upload.";
            return RedirectToPage(new { tab = "System" });
        }

        var (isValid, error) = await _branding.ValidateLogoAsync(logoFile);
        if (!isValid)
        {
            ErrorMessage = error;
            return RedirectToPage(new { tab = "System" });
        }

        try
        {
            var currentUserId = _userManager.GetUserId(User);
            await _branding.UploadLogoAsync(logoFile, User.Identity?.Name);
            await _auditService.LogAsync("SchoolBrandingUpdated", null, currentUserId,
                details: $"keys: Branding:SchoolLogoPath by {User.Identity?.Name}");
            StatusMessage = "School logo uploaded successfully.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Logo upload failed");
            ErrorMessage = "Logo upload failed. Please try again.";
        }

        return RedirectToPage(new { tab = "System" });
    }

    public async Task<IActionResult> OnPostRemoveLogoAsync()
    {
        var currentUserId = _userManager.GetUserId(User);
        await _branding.RemoveLogoAsync(User.Identity?.Name);
        await _auditService.LogAsync("SchoolBrandingUpdated", null, currentUserId,
            details: $"keys: Branding:SchoolLogoPath (removed) by {User.Identity?.Name}");
        StatusMessage = "School logo removed.";
        return RedirectToPage(new { tab = "System" });
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

public class BrandingTabForm
{
    public string? SchoolAddress { get; set; }
    public string? ReturnAddressText { get; set; }
}
