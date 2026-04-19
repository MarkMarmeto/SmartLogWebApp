using System.IO.Ports;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SmartLog.Web.Services;
using SmartLog.Web.Services.Sms;

namespace SmartLog.Web.Pages.Admin.Sms;

[Authorize(Policy = "RequireAdmin")]
public class SettingsModel : PageModel
{
    private readonly ISmsSettingsService _settingsService;
    private readonly IAppSettingsService _appSettingsService;
    private readonly IConfiguration _configuration;
    private readonly GsmModemGateway _gsmGateway;
    private readonly SemaphoreGateway _semaphoreGateway;
    private readonly ILogger<SettingsModel> _logger;

    public SettingsModel(
        ISmsSettingsService settingsService,
        IAppSettingsService appSettingsService,
        IConfiguration configuration,
        GsmModemGateway gsmGateway,
        SemaphoreGateway semaphoreGateway,
        ILogger<SettingsModel> logger)
    {
        _settingsService = settingsService;
        _appSettingsService = appSettingsService;
        _configuration = configuration;
        _gsmGateway = gsmGateway;
        _semaphoreGateway = semaphoreGateway;
        _logger = logger;
    }

    [BindProperty]
    public bool SmsEnabled { get; set; }

    [BindProperty]
    public string DefaultProvider { get; set; } = "GSM_MODEM";

    [BindProperty]
    public bool FallbackEnabled { get; set; }

    [BindProperty]
    public string GsmPortName { get; set; } = "COM3";

    [BindProperty]
    public int GsmBaudRate { get; set; } = 9600;

    [BindProperty]
    public int GsmSendDelay { get; set; } = 3000;

    [BindProperty]
    public string SemaphoreApiKey { get; set; } = string.Empty;

    [BindProperty]
    public string SemaphoreSenderName { get; set; } = "SmartLog";

    [BindProperty]
    public int MaxRetries { get; set; } = 3;

    [BindProperty]
    public int PollingInterval { get; set; } = 5;

    [BindProperty]
    public string NoScanAlertTime { get; set; } = "18:10";

    [BindProperty]
    public bool NoScanAlertEnabled { get; set; } = true;

    [BindProperty]
    public string NoScanAlertProvider { get; set; } = "SEMAPHORE";

    [BindProperty]
    public string? TestPhoneNumber { get; set; }

    public GatewayHealthStatus GsmHealth { get; set; } = new();
    public GatewayHealthStatus SemaphoreHealth { get; set; } = new();
    public string[] AvailablePorts { get; set; } = Array.Empty<string>();

    [TempData]
    public string? StatusMessage { get; set; }

    [TempData]
    public string? ErrorMessage { get; set; }

    public async Task OnGetAsync()
    {
        // Load settings from configuration
        SmsEnabled = _configuration.GetValue<bool>("Sms:Enabled", true);
        DefaultProvider = _configuration.GetValue<string>("Sms:DefaultProvider", "SEMAPHORE") ?? "SEMAPHORE";
        FallbackEnabled = _configuration.GetValue<bool>("Sms:FallbackEnabled", true);

        GsmPortName = await _settingsService.GetSettingAsync("Sms.GsmModem.PortName")
            ?? _configuration.GetValue<string>("Sms:GsmModem:PortName", "COM3") ?? "COM3";
        var dbBaudRate = await _settingsService.GetSettingAsync("Sms.GsmModem.BaudRate");
        GsmBaudRate = dbBaudRate != null && int.TryParse(dbBaudRate, out var parsedBaud)
            ? parsedBaud
            : _configuration.GetValue<int>("Sms:GsmModem:BaudRate", 9600);
        var dbSendDelay = await _settingsService.GetSettingAsync("Sms.GsmModem.SendDelayMs");
        GsmSendDelay = dbSendDelay != null && int.TryParse(dbSendDelay, out var parsedDelay)
            ? parsedDelay
            : _configuration.GetValue<int>("Sms:GsmModem:SendDelayMs", 3000);

        SemaphoreApiKey = await _settingsService.GetSettingAsync("Sms.Semaphore.ApiKey")
            ?? _configuration.GetValue<string>("Sms:Semaphore:ApiKey", "") ?? "";
        SemaphoreSenderName = await _settingsService.GetSettingAsync("Sms.Semaphore.SenderName")
            ?? _configuration.GetValue<string>("Sms:Semaphore:SenderName", "SmartLog") ?? "SmartLog";

        MaxRetries = _configuration.GetValue<int>("Sms:Queue:MaxRetries", 3);
        PollingInterval = _configuration.GetValue<int>("Sms:Queue:PollingIntervalSeconds", 5);

        NoScanAlertTime = await _appSettingsService.GetAsync("Sms:NoScanAlertTime") ?? "18:10";

        var alertEnabledStr = await _appSettingsService.GetAsync("Sms:NoScanAlertEnabled");
        NoScanAlertEnabled = alertEnabledStr == null || !alertEnabledStr.Equals("false", StringComparison.OrdinalIgnoreCase);

        NoScanAlertProvider = await _appSettingsService.GetAsync("Sms:NoScanAlertProvider") ?? "SEMAPHORE";

        // Also check database settings
        var dbEnabled = await _settingsService.GetSettingAsync("Sms.Enabled");
        if (dbEnabled != null)
        {
            SmsEnabled = dbEnabled.Equals("true", StringComparison.OrdinalIgnoreCase) || dbEnabled == "1";
        }

        var dbDefaultProvider = await _settingsService.GetSettingAsync("Sms.DefaultProvider");
        if (dbDefaultProvider != null)
        {
            DefaultProvider = dbDefaultProvider;
        }

        var dbFallbackEnabled = await _settingsService.GetSettingAsync("Sms.FallbackEnabled");
        if (dbFallbackEnabled != null)
        {
            FallbackEnabled = dbFallbackEnabled.Equals("true", StringComparison.OrdinalIgnoreCase) || dbFallbackEnabled == "1";
        }

        // Detect available serial ports
        try
        {
            AvailablePorts = SerialPort.GetPortNames();
        }
        catch
        {
            AvailablePorts = Array.Empty<string>();
        }

        // GSM health is local (no rate limit) — check on every page load
        GsmHealth = await _gsmGateway.GetHealthStatusAsync();
        // Semaphore health is on-demand via button (rate limited: 2 req/min)
    }

    /// <summary>
    /// GET ?handler=SemaphoreStatus — called on demand by the Check Status button.
    /// Returns GatewayHealthStatus as JSON. Rate limit: 2 req/min on Semaphore's side.
    /// </summary>
    public async Task<IActionResult> OnGetSemaphoreStatusAsync()
    {
        try
        {
            var health = await _semaphoreGateway.GetHealthStatusAsync();
            return new JsonResult(new
            {
                isHealthy = health.IsHealthy,
                status = health.Status,
                details = health.Details,
                checkedAt = DateTime.Now.ToString("MMM d, yyyy h:mm:ss tt")
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking Semaphore status");
            return new JsonResult(new
            {
                isHealthy = false,
                status = "Error",
                details = new Dictionary<string, string> { { "Error", ex.Message } },
                checkedAt = DateTime.Now.ToString("MMM d, yyyy h:mm:ss tt")
            });
        }
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!TimeOnly.TryParse(NoScanAlertTime, out _))
        {
            ErrorMessage = "Please enter a valid time in HH:mm format";
            await OnGetAsync();
            return Page();
        }

        try
        {
            // Save settings to database
            await _settingsService.SetSettingAsync("Sms.Enabled", SmsEnabled.ToString().ToLower(), "General");
            await _settingsService.SetSettingAsync("Sms.DefaultProvider", DefaultProvider, "General");
            await _settingsService.SetSettingAsync("Sms.FallbackEnabled", FallbackEnabled.ToString().ToLower(), "General");

            await _settingsService.SetSettingAsync("Sms.GsmModem.PortName", GsmPortName, "GSM");
            await _settingsService.SetSettingAsync("Sms.GsmModem.BaudRate", GsmBaudRate.ToString(), "GSM");
            await _settingsService.SetSettingAsync("Sms.GsmModem.SendDelayMs", GsmSendDelay.ToString(), "GSM");

            await _settingsService.SetSettingAsync("Sms.Semaphore.ApiKey", SemaphoreApiKey, "Cloud");
            await _settingsService.SetSettingAsync("Sms.Semaphore.SenderName", SemaphoreSenderName, "Cloud");

            await _settingsService.SetSettingAsync("Sms.Queue.MaxRetries", MaxRetries.ToString(), "General");
            await _settingsService.SetSettingAsync("Sms.Queue.PollingIntervalSeconds", PollingInterval.ToString(), "General");

            await _appSettingsService.SetAsync("Sms:NoScanAlertTime", NoScanAlertTime, "Sms", null);
            await _appSettingsService.SetAsync("Sms:NoScanAlertEnabled", NoScanAlertEnabled.ToString().ToLower(), "Sms", null);
            await _appSettingsService.SetAsync("Sms:NoScanAlertProvider", NoScanAlertProvider, "Sms", null);

            StatusMessage = "Settings saved successfully. Note: Some settings require application restart to take effect.";
            _logger.LogInformation("SMS settings updated");
        }
        catch (Exception ex)
        {
            ErrorMessage = "Failed to save settings.";
            _logger.LogError(ex, "Error saving SMS settings");
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostTestConnectionAsync()
    {
        try
        {
            var gsmHealth = await _gsmGateway.GetHealthStatusAsync();
            var semaphoreHealth = await _semaphoreGateway.GetHealthStatusAsync();

            if (DefaultProvider == "GSM_MODEM")
            {
                if (gsmHealth.IsHealthy)
                {
                    StatusMessage = $"GSM Modem connection successful. Status: {gsmHealth.Status}";
                }
                else
                {
                    ErrorMessage = $"GSM Modem connection failed. Status: {gsmHealth.Status}";
                }
            }
            else
            {
                if (semaphoreHealth.IsHealthy)
                {
                    StatusMessage = $"Semaphore connection successful. Status: {semaphoreHealth.Status}";
                }
                else
                {
                    ErrorMessage = $"Semaphore connection failed. Status: {semaphoreHealth.Status}";
                }
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Connection test failed: {ex.Message}";
            _logger.LogError(ex, "Error testing SMS connection");
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostSendTestSmsAsync()
    {
        if (string.IsNullOrWhiteSpace(TestPhoneNumber))
        {
            ErrorMessage = "Phone number is required for test SMS.";
            return RedirectToPage();
        }

        try
        {
            var testMessage = $"[SmartLog] Test SMS sent at {DateTime.Now:MMM dd, yyyy h:mm:ss tt}. If you received this, your SMS gateway is working.";

            SmsSendResult result;
            string provider;

            // Send directly through gateway (bypass queue)
            if (DefaultProvider == "SEMAPHORE")
            {
                result = await _semaphoreGateway.SendAsync(TestPhoneNumber, testMessage);
                provider = "Semaphore";
            }
            else
            {
                result = await _gsmGateway.SendAsync(TestPhoneNumber, testMessage);
                provider = "GSM Modem";
            }

            if (result.Success)
            {
                StatusMessage = $"Test SMS sent successfully via {provider} to {TestPhoneNumber} in {result.ProcessingTimeMs}ms. Message ID: {result.ProviderMessageId ?? "N/A"}";
                _logger.LogInformation("Test SMS sent via {Provider} to {Phone}", provider, TestPhoneNumber);
            }
            else
            {
                ErrorMessage = $"Test SMS failed via {provider}: {result.ErrorMessage}";
                _logger.LogWarning("Test SMS failed via {Provider}: {Error}", provider, result.ErrorMessage);
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Test SMS failed: {ex.Message}";
            _logger.LogError(ex, "Error sending test SMS to {Phone}", TestPhoneNumber);
        }

        return RedirectToPage();
    }

    public IActionResult OnGetDetectPorts()
    {
        try
        {
            var ports = SerialPort.GetPortNames();
            return new JsonResult(new { ports, count = ports.Length });
        }
        catch (Exception ex)
        {
            return new JsonResult(new { ports = Array.Empty<string>(), count = 0, error = ex.Message });
        }
    }
}
