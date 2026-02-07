namespace SmartLog.Web.Services.Sms;

/// <summary>
/// Gateway interface for SMS providers (GSM Modem, Semaphore, etc.)
/// </summary>
public interface ISmsGateway
{
    /// <summary>
    /// Provider name (GSM_MODEM, SEMAPHORE)
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// Check if the gateway is currently available
    /// </summary>
    Task<bool> IsAvailableAsync();

    /// <summary>
    /// Send an SMS message
    /// </summary>
    Task<SmsSendResult> SendAsync(string phoneNumber, string message);

    /// <summary>
    /// Get gateway health status
    /// </summary>
    Task<GatewayHealthStatus> GetHealthStatusAsync();
}

/// <summary>
/// Result of SMS send operation
/// </summary>
public class SmsSendResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public string? ProviderMessageId { get; set; }
    public int ProcessingTimeMs { get; set; }
    public int MessageParts { get; set; } = 1;
}

/// <summary>
/// Gateway health status information
/// </summary>
public class GatewayHealthStatus
{
    public bool IsHealthy { get; set; }
    public string Status { get; set; } = string.Empty;
    public Dictionary<string, string> Details { get; set; } = new();
}
