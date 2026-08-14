using System.Collections.Concurrent;

namespace REIGN.API.Calendar;

public class SimulatedCalendarEvent
{
    public string EventId { get; set; } = Guid.NewGuid().ToString("N");

    public Guid AppointmentId { get; set; }

    public string Summary { get; set; } = "";

    public string Description { get; set; } = "";

    public DateTime Start { get; set; }

    public DateTime End { get; set; }

    public string Status { get; set; } = "";

    public bool Cancelled { get; set; }
}

public class SimulatedCalendarService : ICalendarService
{
    private readonly ConcurrentDictionary<string, SimulatedCalendarEvent> _events = new();

    public string ProviderName => "Simulated";

    public bool IsConfigured => true;

    public bool IsSimulated => true;

    public bool HasStoredGrant => true;

    public IReadOnlyCollection<SimulatedCalendarEvent> Events => _events.Values.ToArray();

    public Task<CalendarSyncResult> UpsertAppointmentAsync(CalendarEventRequest request, CancellationToken cancellationToken = default)
    {
        var id = string.IsNullOrWhiteSpace(request.ExistingEventId)
            ? Guid.NewGuid().ToString("N")
            : request.ExistingEventId;

        _events[id] = new SimulatedCalendarEvent
        {
            EventId = id,
            AppointmentId = request.AppointmentId,
            Summary = request.Summary,
            Description = request.Description,
            Start = request.Start,
            End = request.End,
            Status = request.Status,
            Cancelled = false
        };

        return Task.FromResult(CalendarSyncResult.Ok(ProviderName, id, simulated: true));
    }

    public Task<CalendarSyncResult> CancelAppointmentAsync(string? eventId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(eventId))
        {
            return Task.FromResult(CalendarSyncResult.Ok(ProviderName, null, simulated: true));
        }

        if (_events.TryGetValue(eventId, out var existing))
        {
            existing.Cancelled = true;
            existing.Status = "Cancelled";
        }

        return Task.FromResult(CalendarSyncResult.Ok(ProviderName, eventId, simulated: true));
    }
}
