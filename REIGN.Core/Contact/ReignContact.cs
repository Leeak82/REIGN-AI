namespace REIGN.Core.Contact;

/// <summary>
/// Canonical customer-facing contact for REIGN / Miss Reign.
/// The business SMS number is the live SkipCalls inbox, never the owner cell.
/// Public copy uses Miss Reign only. Never put a legal name in SMS, pages, or UI.
/// </summary>
public static class ReignContact
{
    public const string BusinessPhoneE164 = "+18136380375";

    public const string BusinessPhoneNational = "8136380375";

    public const string BusinessPhoneDisplay = "(813) 638-0375";

    // The former customer SMS number remains the voice-forwarding number and
    // the optional Android/SmsGate SIM. It must never replace the SkipCalls
    // number in customer-facing SMS copy while SkipCalls is active.
    public const string VoicePhoneE164 = "+19073001244";

    public const string VoicePhoneDisplay = "(907) 300-1244";

    public const string PublicName = "Miss Reign";

    public const string PublicEmail = "hello@reign.ai";

    /// <summary>
    /// Internal Google Calendar id for booked visits. Not a public-facing name.
    /// </summary>
    public const string ProviderCalendar = "j.collins2491@gmail.com";

    /// <summary>
    /// Google account used to write booked visits. Falls back to the internal
    /// calendar id when the configured id is "primary" or otherwise not an email.
    /// </summary>
    public static string CalendarAccountForDisplay(string? calendarId)
    {
        var value = calendarId?.Trim() ?? "";
        return value.Contains('@', StringComparison.Ordinal) ? value : ProviderCalendar;
    }

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

        if (digits.Length != 10)
        {
            return false;
        }

        var npa = digits[..3];
        var exchange = digits[3..6];
        var line = digits[6..];
        return npa == "555" || (exchange == "555" && line.StartsWith("01", StringComparison.Ordinal));
    }
}
