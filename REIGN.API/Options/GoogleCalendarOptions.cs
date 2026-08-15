namespace REIGN.API.Options;

public class GoogleCalendarOptions
{
    public const string SectionName = "GoogleCalendar";

    /// <summary>
    /// Simulated (default) or Google.
    /// </summary>
    public string Provider { get; set; } = "Simulated";

    public string ClientId { get; set; } = "";

    public string ClientSecret { get; set; } = "";

    public string RedirectUri { get; set; } = "https://localhost:5001/api/integrations/google/callback";

    public string CalendarId { get; set; } = "primary";

    public string TimeZone { get; set; } = "America/New_York";

    public string ApplicationName { get; set; } = "REIGN-AI";
}
