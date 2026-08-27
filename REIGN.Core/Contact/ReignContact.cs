namespace REIGN.Core.Contact;

/// <summary>
/// Canonical customer-facing contact for REIGN / Miss Reign.
/// The business SMS number is a dedicated Straight Talk SIM, never the owner cell.
/// </summary>
public static class ReignContact
{
    public const string BusinessPhoneE164 = "+19073001244";

    public const string BusinessPhoneNational = "9073001244";

    public const string BusinessPhoneDisplay = "(907) 300-1244";

    /// <summary>
    /// True when the value is missing or a reserved 555 fictional number.
    /// </summary>
    public static bool IsPlaceholder(string? number)
    {
        if (string.IsNullOrWhiteSpace(number))
        {
            return true;
        }

        var digits = new string(number.Where(char.IsDigit).ToArray());
        if (digits.Length == 11 && digits[0] == '1')
        {
            digits = digits[1..];
        }

        return digits.Length == 10 && digits.AsSpan(3, 3).SequenceEqual("555");
    }
}
