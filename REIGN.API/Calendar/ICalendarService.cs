namespace REIGN.API.Calendar;

public class CalendarEventRequest
{
    public Guid AppointmentId { get; set; }

    public string Summary { get; set; } = "";

    public string Description { get; set; } = "";

    public DateTime Start { get; set; }

    public DateTime End { get; set; }

    public string Status { get; set; } = "Pending";

    public string? ExistingEventId { get; set; }
}

public sealed class CalendarBusyPeriod
{
    public DateTime Start { get; set; }

    public DateTime End { get; set; }

    public string? EventId { get; set; }
}

public sealed class CalendarAvailabilityResult
{
    public bool Succeeded { get; set; }

    public string Provider { get; set; } = "";

    public string? Error { get; set; }

    public List<CalendarBusyPeriod> Busy { get; set; } = [];

    public static CalendarAvailabilityResult Ok(string provider, IEnumerable<CalendarBusyPeriod>? busy = null) =>
        new()
        {
            Succeeded = true,
            Provider = provider,
            Busy = busy?.ToList() ?? []
        };

    public static CalendarAvailabilityResult Fail(string provider, string error) =>
        new()
        {
            Succeeded = false,
            Provider = provider,
            Error = error
        };
}

public class CalendarSyncResult
{
    public bool Succeeded { get; set; }

    public bool Simulated { get; set; }

    public string Provider { get; set; } = "";

    public string? EventId { get; set; }

    public string? Error { get; set; }

    public string? HtmlLink { get; set; }

    public string? TimeZone { get; set; }

    public string? CalendarId { get; set; }

    public int? GoogleStatusCode { get; set; }

    public static CalendarSyncResult Ok(
        string provider,
        string? eventId,
        bool simulated = false,
        string? htmlLink = null,
        string? timeZone = null,
        string? calendarId = null,
        int? googleStatusCode = null) =>
        new()
        {
            Succeeded = true,
            Simulated = simulated,
            Provider = provider,
            EventId = eventId,
            HtmlLink = htmlLink,
            TimeZone = timeZone,
            CalendarId = calendarId,
            GoogleStatusCode = googleStatusCode
        };

    public static CalendarSyncResult Fail(
        string provider,
        string error,
        bool simulated = false,
        int? googleStatusCode = null,
        string? calendarId = null) =>
        new()
        {
            Succeeded = false,
            Simulated = simulated,
            Provider = provider,
            Error = error,
            GoogleStatusCode = googleStatusCode,
            CalendarId = calendarId
        };
}

public interface ICalendarService
{
    string ProviderName { get; }

    bool IsConfigured { get; }

    bool IsSimulated { get; }

    bool HasStoredGrant { get; }

    Task<CalendarSyncResult> UpsertAppointmentAsync(CalendarEventRequest request, CancellationToken cancellationToken = default);

    Task<CalendarSyncResult> CancelAppointmentAsync(string? eventId, CancellationToken cancellationToken = default);

    Task<CalendarAvailabilityResult> GetBusyPeriodsAsync(
        DateTime start,
        DateTime end,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(CalendarAvailabilityResult.Fail(ProviderName, "Calendar busy lookup is not implemented by this provider."));
}
