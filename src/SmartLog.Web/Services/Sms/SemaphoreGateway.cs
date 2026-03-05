using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace SmartLog.Web.Services.Sms;

/// <summary>
/// Semaphore cloud SMS gateway
/// Requires internet connection
/// </summary>
public class SemaphoreGateway : ISmsGateway
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<SemaphoreGateway> _logger;
    private readonly HttpClient _httpClient;

    private const string BaseUrl = "https://api.semaphore.co/api/v4";

    public string ProviderName => "SEMAPHORE";

    public SemaphoreGateway(
        IConfiguration configuration,
        ILogger<SemaphoreGateway> logger,
        IHttpClientFactory httpClientFactory)
    {
        _configuration = configuration;
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient();
        _httpClient.Timeout = TimeSpan.FromSeconds(30);
    }

    public async Task<bool> IsAvailableAsync()
    {
        try
        {
            var apiKey = _configuration.GetValue<string>("Sms:Semaphore:ApiKey");
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                _logger.LogWarning("Semaphore API key not configured");
                return false;
            }

            // Test connectivity with account balance check
            var health = await GetHealthStatusAsync();
            return health.IsHealthy;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Semaphore gateway not available");
            return false;
        }
    }

    public async Task<SmsSendResult> SendAsync(string phoneNumber, string message)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var apiKey = _configuration.GetValue<string>("Sms:Semaphore:ApiKey");
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                throw new InvalidOperationException("Semaphore API key not configured");
            }

            var senderName = _configuration.GetValue<string>("Sms:Semaphore:SenderName", "SmartLog") ?? "SmartLog";

            // Normalize phone number
            var normalizedPhone = NormalizePhoneNumber(phoneNumber);

            // Prepare request
            var requestData = new Dictionary<string, string>
            {
                { "apikey", apiKey },
                { "number", normalizedPhone },
                { "message", message },
                { "sendername", senderName }
            };

            var content = new FormUrlEncodedContent(requestData);

            // Send request
            var response = await _httpClient.PostAsync($"{BaseUrl}/messages", content);
            var responseBody = await response.Content.ReadAsStringAsync();

            stopwatch.Stop();

            if (response.IsSuccessStatusCode)
            {
                var result = JsonSerializer.Deserialize<SemaphoreResponse>(responseBody);
                var messageParts = CalculateMessageParts(message);

                _logger.LogInformation("SMS sent via Semaphore to {Phone} in {Ms}ms",
                    normalizedPhone, stopwatch.ElapsedMilliseconds);

                return new SmsSendResult
                {
                    Success = true,
                    ProviderMessageId = result?.MessageId?.ToString(),
                    ProcessingTimeMs = (int)stopwatch.ElapsedMilliseconds,
                    MessageParts = messageParts
                };
            }
            else
            {
                _logger.LogError("Semaphore API error: {StatusCode} - {Response}",
                    response.StatusCode, responseBody);

                return new SmsSendResult
                {
                    Success = false,
                    ErrorMessage = $"API error: {response.StatusCode} - {responseBody}",
                    ProcessingTimeMs = (int)stopwatch.ElapsedMilliseconds
                };
            }
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Error sending SMS via Semaphore to {Phone}", phoneNumber);

            return new SmsSendResult
            {
                Success = false,
                ErrorMessage = ex.Message,
                ProcessingTimeMs = (int)stopwatch.ElapsedMilliseconds
            };
        }
    }

    public async Task<GatewayHealthStatus> GetHealthStatusAsync()
    {
        var status = new GatewayHealthStatus
        {
            IsHealthy = false,
            Status = "Offline",
            Details = new Dictionary<string, string>()
        };

        try
        {
            var apiKey = _configuration.GetValue<string>("Sms:Semaphore:ApiKey");
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                status.Details["Error"] = "API key not configured";
                return status;
            }

            // Check account balance using POST to avoid API key in URL/logs
            var formData = new FormUrlEncodedContent(new[] { new KeyValuePair<string, string>("apikey", apiKey) });
            var response = await _httpClient.PostAsync($"{BaseUrl}/account", formData);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var accountInfo = JsonSerializer.Deserialize<SemaphoreAccountResponse>(responseBody);

                status.IsHealthy = true;
                status.Status = "Online";
                status.Details["Balance"] = accountInfo?.Balance.ToString("N2") ?? "Unknown";
                status.Details["SenderName"] = _configuration.GetValue<string>("Sms:Semaphore:SenderName", "SmartLog") ?? "SmartLog";

                if (accountInfo != null && accountInfo.Balance < 10)
                {
                    status.Details["Warning"] = "Low balance";
                }
            }
            else
            {
                status.Details["Error"] = $"API error: {response.StatusCode}";
                status.Status = "API error";
            }

            return status;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking Semaphore health");
            status.Details["Error"] = ex.Message;
            return status;
        }
    }

    private string NormalizePhoneNumber(string phoneNumber)
    {
        // Remove all non-digit characters
        var digits = new string(phoneNumber.Where(char.IsDigit).ToArray());

        // Convert 09xxxxxxxxx to 639xxxxxxxxx (Semaphore doesn't use + prefix)
        if (digits.StartsWith("09") && digits.Length == 11)
        {
            return "63" + digits.Substring(1);
        }

        // Remove + if present
        if (phoneNumber.StartsWith("+"))
        {
            return digits;
        }

        return digits;
    }

    private int CalculateMessageParts(string message)
    {
        // Standard SMS is 160 chars, multipart is 153 chars per part
        var length = message.Length;
        if (length <= 160) return 1;
        return (int)Math.Ceiling((double)length / 153);
    }

    private class SemaphoreResponse
    {
        public int[]? MessageId { get; set; }
        public string? Status { get; set; }
    }

    private class SemaphoreAccountResponse
    {
        public string? AccountName { get; set; }
        public decimal Balance { get; set; }
        public string? Status { get; set; }
    }
}
