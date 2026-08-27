using System.Text.RegularExpressions;
using REIGN.Core.Contact;

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

    public static string FormatDisplay(string? value)
    {
        var normalized = Normalize(value);
        var digits = new string(normalized.Where(char.IsDigit).ToArray());
        if (digits.Length == 11 && digits[0] == '1')
        {
            digits = digits[1..];
        }

        if (digits.Length == 10)
        {
            return $"({digits[..3]}) {digits[3..6]}-{digits[6..]}";
        }

        return string.IsNullOrWhiteSpace(normalized) ? "" : normalized;
    }

    public static bool AreSame(string? left, string? right)
    {
        var a = Normalize(left);
        var b = Normalize(right);
        return !string.IsNullOrWhiteSpace(a) && string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsShortCode(string? value)
    {
        var digits = new string(Normalize(value).Where(char.IsDigit).ToArray());
        return digits.Length is > 0 and < 10;
    }

    /// <summary>
    /// True for a real customer handset. False for short codes (611611),
    /// fictional 555 numbers, and the business SIM itself.
    /// </summary>
    public static bool IsReplyableCustomerNumber(string? value, string? businessNumber = null)
    {
        var normalized = Normalize(value);
        if (string.IsNullOrWhiteSpace(normalized) || IsShortCode(normalized))
        {
            return false;
        }

        if (ReignContact.IsPlaceholder(normalized) || AreSame(normalized, ReignContact.BusinessPhoneE164))
        {
            return false;
        }

        return string.IsNullOrWhiteSpace(businessNumber) || !AreSame(normalized, businessNumber);
    }
}
