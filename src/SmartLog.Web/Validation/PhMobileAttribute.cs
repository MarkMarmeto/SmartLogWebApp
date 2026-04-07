using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace SmartLog.Web.Validation;

/// <summary>
/// Validates Philippine mobile number format.
/// Accepts: 09XXXXXXXXX (local) or +639XXXXXXXXX / 639XXXXXXXXX (international).
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class PhMobileAttribute : ValidationAttribute
{
    public PhMobileAttribute()
    {
        ErrorMessage = "Please enter a valid Philippine mobile number (e.g. 09171234567).";
    }

    public override bool IsValid(object? value)
    {
        if (value is null) return true; // Use [Required] separately for mandatory fields
        var phone = value.ToString() ?? string.Empty;
        return IsValidPhMobile(phone);
    }

    public static bool IsValidPhMobile(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return false;
        var digits = Regex.Replace(phone, @"\D", "");
        // Local: 09XXXXXXXXX (11 digits)
        if (Regex.IsMatch(digits, @"^09\d{9}$")) return true;
        // International without +: 639XXXXXXXXX (12 digits)
        if (Regex.IsMatch(digits, @"^639\d{9}$")) return true;
        return false;
    }

    /// <summary>
    /// Normalizes to +639XXXXXXXXX format. Returns null if invalid.
    /// </summary>
    public static string? Normalize(string? phone)
    {
        if (!IsValidPhMobile(phone)) return null;
        var digits = Regex.Replace(phone!, @"\D", "");
        if (digits.StartsWith("09")) return "+63" + digits[1..];
        if (digits.StartsWith("639")) return "+" + digits;
        return null;
    }
}
