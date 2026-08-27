using Microsoft.Extensions.Options;
using REIGN.API.Configuration;
using REIGN.API.Options;

namespace REIGN.API.Calendar;

public class ConfigurableCalendarService : ICalendarService
{
    private readonly ICalendarService _inner;
    private readonly GoogleCalendarService _google;

    public ConfigurableCalendarService(
        IOptions<GoogleCalendarOptions> options,
        SimulatedCalendarService simulated,
        GoogleCalendarService google,
        IHostEnvironment environment)
    {
        _google = google;
        var provider = CalendarProviderSelection.Resolve(
            options.Value.Provider,
            environment.IsDevelopment());
        if (!environment.IsDevelopment() && CalendarProviderSelection.IsSimulated(provider))
        {
            throw new InvalidOperationException(
                "Production cannot use Simulated Calendar. Set GoogleCalendar__Provider=Google.");
        }

        _inner = provider.Equals("google", StringComparison.OrdinalIgnoreCase)
            ? google
            : environment.IsDevelopment() ? simulated : google;
    }

    public string ProviderName => _inner.ProviderName;

    public bool IsConfigured => _inner.IsConfigured;

    public bool IsSimulated => _inner.IsSimulated;

    public bool HasStoredGrant => _inner.HasStoredGrant;

    public Task<CalendarSyncResult> UpsertAppointmentAsync(CalendarEventRequest request, CancellationToken cancellationToken = default) =>
        _inner.UpsertAppointmentAsync(request, cancellationToken);

    public Task<CalendarSyncResult> CancelAppointmentAsync(string? eventId, CancellationToken cancellationToken = default) =>
        _inner.CancelAppointmentAsync(eventId, cancellationToken);

    public Task StoreAuthorizationCodeAsync(string code, CancellationToken cancellationToken = default) =>
        _google.StoreAuthorizationCodeAsync(code, cancellationToken);

    public Task StoreAuthorizationCodeAsync(string code, string redirectUri, CancellationToken cancellationToken = default) =>
        _google.StoreAuthorizationCodeAsync(code, redirectUri, cancellationToken);
}
