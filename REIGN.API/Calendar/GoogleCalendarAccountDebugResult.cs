namespace REIGN.API.Calendar;

public sealed class GoogleCalendarAccountDebugResult
{
    public string Provider { get; set; } = "Google";

    public string CalendarId { get; set; } = "";

    public string? ResolvedCalendarId { get; set; }

    public string? Email { get; set; }

    public string? CalendarSummary { get; set; }

    public string? TimeZone { get; set; }

    public string RequiredScope { get; set; } = GoogleCalendarService.RequiredScope;

    public string? StoredScope { get; set; }

    public string? LiveScope { get; set; }

    public bool ScopeSufficient { get; set; }

    public bool ReconnectRequired { get; set; }

    public string? ReconnectReason { get; set; }

    public bool OauthClientConfigured { get; set; }

    public bool HasStoredGrant { get; set; }

    public int? GoogleStatusCode { get; set; }

    public string? Error { get; set; }
}
