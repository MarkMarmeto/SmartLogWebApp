using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SmartLog.Web.Services.Sms;

/// <summary>
/// Semaphore cloud SMS gateway (https://semaphore.co/docs)
/// POST /messages — send SMS (rate limited: 120 req/min)
/// GET  /account  — account info and credit balance (rate limited: 2 req/min)
/// </summary>
public class SemaphoreGateway : ISmsGateway
{
    private readonly IConfiguration _configuration;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SemaphoreGateway> _logger;
    private readonly HttpClient _httpClient;

    private const string BaseUrl = "https://api.semaphore.co/api/v4";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public string ProviderName => "SEMAPHORE";

    public SemaphoreGateway(
        IConfiguration configuration,
        IServiceScopeFactory scopeFactory,
        ILogger<SemaphoreGateway> logger,
        IHttpClientFactory httpClientFactory)
    {
        _configuration = configuration;
        _scopeFactory = scopeFactory;
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient();
        _httpClient.Timeout = TimeSpan.FromSeconds(30);
    }

    private async Task<string?> GetApiKeyAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var smsSettings = scope.ServiceProvider.GetRequiredService<ISmsSettingsService>();
        return await smsSettings.GetSettingAsync("Sms.Semaphore.ApiKey")
               ?? _configuration.GetValue<string>("Sms:Semaphore:ApiKey");
    }

    private async Task<string> GetSenderNameAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var smsSettings = scope.ServiceProvider.GetRequiredService<ISmsSettingsService>();
        return await smsSettings.GetSettingAsync("Sms.Semaphore.SenderName")
               ?? _configuration.GetValue<string>("Sms:Semaphore:SenderName", "SmartLog")
               ?? "SmartLog";
    }

    /// <summary>
    /// Returns true when a Semaphore API key is configured.
    /// Does NOT make a live HTTP request — routing decisions must not block on a
    /// rate-limited availability check (Semaphore /account is capped at 2 req/min).
    /// Use GetHealthStatusAsync() for the Settings UI health check only.
    /// </summary>
    public async Task<bool> IsAvailableAsync()
    {
        var apiKey = await GetApiKeyAsync();
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            _logger.LogDebug("Semaphore API key not configured — gateway unavailable");
            return false;
        }

        return true;
    }

    public async Task<SmsSendResult> SendAsync(string phoneNumber, string message)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var apiKey = await GetApiKeyAsync();
            if (string.IsNullOrWhiteSpace(apiKey))
                throw new InvalidOperationException("Semaphore API key not configured");

            var senderName = await GetSenderNameAsync();
            var normalizedPhone = NormalizePhoneNumber(phoneNumber);

            // POST /messages — form-encoded per Semaphore docs
            var requestData = new Dictionary<string, string>
            {
                { "apikey", apiKey },
                { "number", normalizedPhone },
                { "message", message },
                { "sendername", senderName }
            };

            var response = await _httpClient.PostAsync(
                $"{BaseUrl}/messages",
                new FormUrlEncodedContent(requestData));

            var responseBody = await response.Content.ReadAsStringAsync();
            stopwatch.Stop();

            if (response.IsSuccessStatusCode)
            {
                // API returns a JSON array of message objects even for a single recipient
                var results = JsonSerializer.Deserialize<SemaphoreMessage[]>(responseBody, JsonOptions);
                var first = results?.FirstOrDefault();
                var messageParts = CalculateMessageParts(message);

                _logger.LogInformation(
                    "SMS sent via Semaphore to {Phone}, messageId={MessageId}, status={Status}, in {Ms}ms",
                    normalizedPhone, first?.MessageId, first?.Status, stopwatch.ElapsedMilliseconds);

                return new SmsSendResult
                {
                    Success = true,
                    ProviderMessageId = first?.MessageId?.ToString(),
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
                    ErrorMessage = $"HTTP {(int)response.StatusCode}: {responseBody}",
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

    /// <summary>
    /// Sends the same message to multiple recipients in a single API call.
    /// Semaphore accepts up to 1,000 numbers per request (comma-separated).
    /// </summary>
    public async Task<BulkSendResult> SendBulkAsync(IReadOnlyList<string> phoneNumbers, string message)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = new BulkSendResult();

        try
        {
            var apiKey = await GetApiKeyAsync();
            if (string.IsNullOrWhiteSpace(apiKey))
                throw new InvalidOperationException("Semaphore API key not configured");

            var senderName = await GetSenderNameAsync();

            // Semaphore accepts up to 1,000 numbers per request — chunk if needed
            const int batchSize = 1000;
            var batches = phoneNumbers
                .Select(NormalizePhoneNumber)
                .Select((phone, i) => (phone, i))
                .GroupBy(x => x.i / batchSize)
                .Select(g => g.Select(x => x.phone).ToList())
                .ToList();

            foreach (var batch in batches)
            {
                var numbers = string.Join(",", batch);
                var requestData = new Dictionary<string, string>
                {
                    { "apikey", apiKey },
                    { "number", numbers },
                    { "message", message },
                    { "sendername", senderName }
                };

                var response = await _httpClient.PostAsync(
                    $"{BaseUrl}/messages",
                    new FormUrlEncodedContent(requestData));

                var responseBody = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var messages = JsonSerializer.Deserialize<SemaphoreMessage[]>(responseBody, JsonOptions)
                                   ?? Array.Empty<SemaphoreMessage>();

                    foreach (var msg in messages)
                    {
                        result.Results.Add(new BulkSendRecipientResult
                        {
                            PhoneNumber = msg.Recipient ?? string.Empty,
                            Success = true,
                            ProviderMessageId = msg.MessageId?.ToString()
                        });
                    }

                    _logger.LogInformation(
                        "Bulk SMS sent via Semaphore: {Count} recipients in batch, in {Ms}ms",
                        batch.Count, stopwatch.ElapsedMilliseconds);
                }
                else
                {
                    _logger.LogError("Semaphore bulk API error: {StatusCode} - {Response}",
                        response.StatusCode, responseBody);

                    // Mark all in this batch as failed
                    foreach (var phone in batch)
                    {
                        result.Results.Add(new BulkSendRecipientResult
                        {
                            PhoneNumber = phone,
                            Success = false,
                            ErrorMessage = $"HTTP {(int)response.StatusCode}: {responseBody}"
                        });
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending bulk SMS via Semaphore");
            result.ErrorMessage = ex.Message;
        }

        stopwatch.Stop();
        result.ProcessingTimeMs = (int)stopwatch.ElapsedMilliseconds;
        return result;
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
            var apiKey = await GetApiKeyAsync();
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                status.Details["Error"] = "API key not configured";
                return status;
            }

            // GET /account?apikey=... per Semaphore docs (rate limited: 2 req/min)
            var response = await _httpClient.GetAsync($"{BaseUrl}/account?apikey={Uri.EscapeDataString(apiKey)}");
            var responseBody = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var accountInfo = JsonSerializer.Deserialize<SemaphoreAccount>(responseBody, JsonOptions);

                status.IsHealthy = true;
                status.Status = "Online";
                status.Details["AccountName"] = accountInfo?.AccountName ?? "Unknown";
                status.Details["Balance"] = accountInfo?.CreditBalance.ToString("N2") ?? "Unknown";
                status.Details["SenderName"] = await GetSenderNameAsync();

                if (accountInfo != null && accountInfo.CreditBalance < 10)
                    status.Details["Warning"] = "Low credit balance";
            }
            else
            {
                status.Status = $"API error ({(int)response.StatusCode})";
                status.Details["Error"] = responseBody;
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

    /// <summary>
    /// Normalizes Philippine mobile numbers to international format (639XXXXXXXXX).
    /// Semaphore accepts both local (09XXXXXXXXX) and international formats.
    /// </summary>
    private static string NormalizePhoneNumber(string phoneNumber)
    {
        var digits = new string(phoneNumber.Where(char.IsDigit).ToArray());

        // 09XXXXXXXXX → 639XXXXXXXXX
        if (digits.StartsWith("09") && digits.Length == 11)
            return "63" + digits.Substring(1);

        // +639XXXXXXXXX → 639XXXXXXXXX
        if (phoneNumber.StartsWith("+"))
            return digits;

        return digits;
    }

    private static int CalculateMessageParts(string message)
    {
        // Standard SMS: 160 chars; multipart: 153 chars per segment
        if (message.Length <= 160) return 1;
        return (int)Math.Ceiling((double)message.Length / 153);
    }

    // Bulk send result models

    public class BulkSendResult
    {
        public List<BulkSendRecipientResult> Results { get; } = new();
        public string? ErrorMessage { get; set; }
        public int ProcessingTimeMs { get; set; }
        public int SuccessCount => Results.Count(r => r.Success);
        public int FailureCount => Results.Count(r => !r.Success);
    }

    public class BulkSendRecipientResult
    {
        public string PhoneNumber { get; set; } = string.Empty;
        public bool Success { get; set; }
        public string? ProviderMessageId { get; set; }
        public string? ErrorMessage { get; set; }
    }

    // Response models matching Semaphore's actual JSON field names

    private class SemaphoreMessage
    {
        [JsonPropertyName("message_id")]
        public long? MessageId { get; set; }

        [JsonPropertyName("status")]
        public string? Status { get; set; }

        [JsonPropertyName("recipient")]
        public string? Recipient { get; set; }

        [JsonPropertyName("network")]
        public string? Network { get; set; }
    }

    private class SemaphoreAccount
    {
        [JsonPropertyName("account_id")]
        public long? AccountId { get; set; }

        [JsonPropertyName("account_name")]
        public string? AccountName { get; set; }

        [JsonPropertyName("credit_balance")]
        public decimal CreditBalance { get; set; }

        [JsonPropertyName("status")]
        public string? Status { get; set; }
    }
}
