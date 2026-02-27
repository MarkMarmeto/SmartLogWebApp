using System.Security.Cryptography;
using System.Text;

namespace SmartLog.Web.Services;

public static class PasswordGenerator
{
    private const string Lowercase = "abcdefghijklmnopqrstuvwxyz";
    private const string Uppercase = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
    private const string Digits = "0123456789";
    private const string Special = "!@#$%";
    private const string All = Lowercase + Uppercase + Digits + Special;

    public static string GenerateTemporaryPassword(int length = 12)
    {
        var password = new StringBuilder(length);

        // Ensure at least one of each required type
        password.Append(Uppercase[RandomNumberGenerator.GetInt32(Uppercase.Length)]);
        password.Append(Lowercase[RandomNumberGenerator.GetInt32(Lowercase.Length)]);
        password.Append(Digits[RandomNumberGenerator.GetInt32(Digits.Length)]);
        password.Append(Special[RandomNumberGenerator.GetInt32(Special.Length)]);

        // Fill remaining with random characters
        for (int i = 4; i < length; i++)
        {
            password.Append(All[RandomNumberGenerator.GetInt32(All.Length)]);
        }

        // Shuffle using Fisher-Yates with cryptographic RNG
        var chars = password.ToString().ToCharArray();
        for (int i = chars.Length - 1; i > 0; i--)
        {
            int j = RandomNumberGenerator.GetInt32(i + 1);
            (chars[i], chars[j]) = (chars[j], chars[i]);
        }

        return new string(chars);
    }
}
