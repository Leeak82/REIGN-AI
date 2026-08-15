namespace REIGN.API.Configuration;

/// <summary>
/// Simulated SMS is a Development/test provider only. Production defaults to Twilio
/// unless SMS_PROVIDER/Sms:Provider is Twilio or Vonage.
/// </summary>
public static class SmsProviderSelection
{
    public const string Simulated = "Simulated";
    public const string Twilio = "Twilio";
    public const string Vonage = "Vonage";

    public static string Resolve(string? configured, bool isDevelopment)
    {
        var provider = configured?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(provider))
        {
            return isDevelopment ? Simulated : Twilio;
        }

        if (!isDevelopment && provider.Equals(Simulated, StringComparison.OrdinalIgnoreCase))
        {
            return Twilio;
        }

        return provider;
    }

    public static bool IsSimulated(string? provider) =>
        string.IsNullOrWhiteSpace(provider)
        || provider.Equals(Simulated, StringComparison.OrdinalIgnoreCase);
}
