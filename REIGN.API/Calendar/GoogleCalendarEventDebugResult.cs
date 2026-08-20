namespace REIGN.API.Calendar;

public sealed class GoogleCalendarEventDebugResult
{
    public string Provider { get; set; } = "Google";

    public string CalendarId { get; set; } = "";

    public string EventId { get; set; } = "";

    public bool Found { get; set; }

    public int? GoogleStatusCode { get; set; }

    public string? Summary { get; set; }

    public string? Description { get; set; }

    public string? Start { get; set; }

    public string? End { get; set; }

    public string? TimeZone { get; set; }

    public string? HtmlLink { get; set; }

    public string? OrganizerEmail { get; set; }

    public string? CreatorEmail { get; set; }

    public string? Status { get; set; }

    public string? Error { get; set; }
}
