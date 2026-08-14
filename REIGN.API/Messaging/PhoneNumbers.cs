using System.Text.RegularExpressions;

namespace REIGN.API.Messaging;

public static class PhoneNumbers
{
    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "";
        }

        var digits = Regex.Replace(value, @"[^\d+]", "");
        if (digits.StartsWith('+'))
        {
            return "+" + Regex.Replace(digits[1..], @"\D", "");
        }

        var onlyDigits = Regex.Replace(digits, @"\D", "");
        if (onlyDigits.Length == 10)
        {
            return "+1" + onlyDigits;
        }

        if (onlyDigits.Length == 11 && onlyDigits.StartsWith('1'))
        {
            return "+" + onlyDigits;
        }

        return onlyDigits.Length > 0 ? onlyDigits : value.Trim();
    }

    public static bool AreSame(string? left, string? right)
    {
        var a = Normalize(left);
        var b = Normalize(right);
        return !string.IsNullOrWhiteSpace(a) && string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
    }
}
