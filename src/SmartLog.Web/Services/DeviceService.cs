using System.Security.Cryptography;
using System.Text;

namespace SmartLog.Web.Services;

/// <summary>
/// Service for device API key management.
/// Implements US0028 (Register Scanner Device).
/// </summary>
public class DeviceService : IDeviceService
{
    /// <summary>
    /// Generates a new API key in format: sk_live_{32-char-base64}
    /// </summary>
    public string GenerateApiKey()
    {
        // Generate 24 random bytes (will become 32 characters when base64 encoded)
        var randomBytes = new byte[24];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(randomBytes);
        }

        // Convert to base64 and make URL-safe
        var base64 = Convert.ToBase64String(randomBytes)
            .Replace("+", "")
            .Replace("/", "")
            .Replace("=", "");

        return $"sk_live_{base64}";
    }

    /// <summary>
    /// Hashes an API key for storage using SHA-256.
    /// </summary>
    public string HashApiKey(string apiKey)
    {
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(apiKey));
        return Convert.ToBase64String(hashBytes);
    }

    /// <summary>
    /// Verifies an API key against a stored hash.
    /// Uses constant-time comparison to prevent timing attacks.
    /// </summary>
    public bool VerifyApiKey(string apiKey, string apiKeyHash)
    {
        var computedHash = HashApiKey(apiKey);
        var computedHashBytes = Convert.FromBase64String(computedHash);
        var storedHashBytes = Convert.FromBase64String(apiKeyHash);

        return CryptographicOperations.FixedTimeEquals(computedHashBytes, storedHashBytes);
    }
}
