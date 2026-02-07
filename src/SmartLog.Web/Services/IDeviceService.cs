namespace SmartLog.Web.Services;

/// <summary>
/// Service for device API key management.
/// </summary>
public interface IDeviceService
{
    /// <summary>
    /// Generates a new API key in format: sk_live_{32-char-base64}
    /// </summary>
    string GenerateApiKey();

    /// <summary>
    /// Hashes an API key for storage using SHA-256.
    /// </summary>
    string HashApiKey(string apiKey);

    /// <summary>
    /// Verifies an API key against a stored hash.
    /// Uses constant-time comparison to prevent timing attacks.
    /// </summary>
    bool VerifyApiKey(string apiKey, string apiKeyHash);
}
