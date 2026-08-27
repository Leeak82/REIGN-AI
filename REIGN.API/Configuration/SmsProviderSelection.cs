using REIGN.API.Messaging;
using REIGN.Core.Contact;

namespace REIGN.API.Configuration;

/// <summary>
/// Simulated SMS is a Development/test provider only. Production defaults to Twilio
/// unless SMS_PROVIDER/Sms:Provider is Twilio, Vonage, SmsGate, or SkipCalls.
/// While A2P is pending, the Straight Talk SIM is the customer-facing number and
/// production uses SmsGate unless that same number has been ported onto Twilio.
/// </summary>
public static class SmsProviderSelection
{
    public const string Simulated = "Simulated";
    public const string Twilio = "Twilio";
    public const string Vonage = "Vonage";
    public const string SmsGate = "SmsGate";

    public static string Resolve(string? configured, bool isDevelopment) =>
        Resolve(configured, isDevelopment, businessNumber: null, twilioFromNumber: null);

    public static string Resolve(
        string? configured,
        bool isDevelopment,
        string? businessNumber,
        string? twilioFromNumber)
    {
        var provider = configured?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(provider))
        {
            provider = isDevelopment ? Simulated : Twilio;
        }
        else if (!isDevelopment && provider.Equals(Simulated, StringComparison.OrdinalIgnoreCase))
        {
            provider = Twilio;
        }

        if (!isDevelopment && UsesStraightTalkSim(businessNumber, twilioFromNumber, provider))
        {
            return SmsGate;
        }

        return provider;
    }

    public static bool IsSimulated(string? provider) =>
        string.IsNullOrWhiteSpace(provider)
        || provider.Equals(Simulated, StringComparison.OrdinalIgnoreCase);

    private static bool UsesStraightTalkSim(string? businessNumber, string? twilioFromNumber, string provider)
    {
        if (!PhoneNumbers.AreSame(businessNumber, ReignContact.BusinessPhoneE164))
        {
            return false;
        }

        if (PhoneNumbers.AreSame(twilioFromNumber, ReignContact.BusinessPhoneE164))
        {
            return false;
        }

        return provider.Equals(Twilio, StringComparison.OrdinalIgnoreCase)
            || provider.Equals(Simulated, StringComparison.OrdinalIgnoreCase);
    }
}
