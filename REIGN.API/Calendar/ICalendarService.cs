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

public class CalendarSyncResult
{
    public bool Succeeded { get; set; }

    public bool Simulated { get; set; }

    public string Provider { get; set; } = "";

    public string? EventId { get; set; }

    public string? Error { get; set; }

    public static CalendarSyncResult Ok(string provider, string? eventId, bool simulated = false) =>
        new()
        {
            Succeeded = true,
            Simulated = simulated,
            Provider = provider,
            EventId = eventId
        };

    public static CalendarSyncResult Fail(string provider, string error, bool simulated = false) =>
        new()
        {
            Succeeded = false,
            Simulated = simulated,
            Provider = provider,
            Error = error
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
}
