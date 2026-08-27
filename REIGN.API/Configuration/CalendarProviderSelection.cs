namespace REIGN.API.Configuration;

/// <summary>
/// Simulated Calendar is a Development/test provider only.
/// Production always uses Google Calendar for live bookings.
/// </summary>
public static class CalendarProviderSelection
{
    public const string Simulated = "Simulated";
    public const string Google = "Google";

    public static string Resolve(string? configured, bool isDevelopment)
    {
        var provider = configured?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(provider))
        {
            return isDevelopment ? Simulated : Google;
        }

        if (!isDevelopment && provider.Equals(Simulated, StringComparison.OrdinalIgnoreCase))
        {
            return Google;
        }

        return provider;
    }

    public static bool IsSimulated(string? provider) =>
        string.IsNullOrWhiteSpace(provider)
        || provider.Equals(Simulated, StringComparison.OrdinalIgnoreCase);
}
