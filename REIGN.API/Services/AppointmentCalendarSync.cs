using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using REIGN.API.Calendar;
using REIGN.API.Options;
using REIGN.Data;
using REIGN.Data.Models;

namespace REIGN.API.Services;

public class AppointmentCalendarSync
{
    private readonly ReignDbContext _db;
    private readonly ICalendarService _calendar;
    private readonly GoogleCalendarOptions _google;
    private readonly ILogger<AppointmentCalendarSync> _logger;

    public AppointmentCalendarSync(
        ReignDbContext db,
        ICalendarService calendar,
        IOptions<GoogleCalendarOptions> google,
        ILogger<AppointmentCalendarSync> logger)
    {
        _db = db;
        _calendar = calendar;
        _google = google.Value;
        _logger = logger;
    }

    public async Task<CalendarSyncResult> SyncAsync(Appointment appointment, CancellationToken cancellationToken = default)
    {
        try
        {
            if (appointment.Service == null)
            {
                await _db.Entry(appointment).Reference(x => x.Service).LoadAsync(cancellationToken);
            }

            if (appointment.Customer == null)
            {
                await _db.Entry(appointment).Reference(x => x.Customer).LoadAsync(cancellationToken);
            }

            var duration = appointment.DurationMinutes > 0
                ? appointment.DurationMinutes
                : appointment.Service?.DurationMinutes ?? 30;

            var customerName = appointment.Customer?.Name?.Trim();
            var customerPhone = appointment.Customer?.PhoneNumber?.Trim();
            var who = !string.IsNullOrWhiteSpace(customerName)
                ? customerName
                : (!string.IsNullOrWhiteSpace(customerPhone) ? customerPhone : "Customer");
            var serviceName = appointment.Service?.Name ?? "Appointment";
            var timeZoneId = CalendarTime.ToGoogleTimeZoneId(
                !string.IsNullOrWhiteSpace(_google.TimeZone)
                    ? _google.TimeZone
                    : await ResolveBusinessTimeZoneAsync(cancellationToken));
            var businessName = await ResolveBusinessNameAsync(cancellationToken);
            var result = await _calendar.UpsertAppointmentAsync(new CalendarEventRequest
            {
                AppointmentId = appointment.Id,
                ExistingEventId = appointment.ExternalCalendarEventId,
                Summary = $"REIGN {serviceName} — {who}",
                Description = BuildEventDescription(
                    businessName,
                    appointment.Status,
                    serviceName,
                    duration,
                    appointment.Price,
                    who,
                    customerPhone,
                    timeZoneId),
                Start = appointment.AppointmentTime,
                End = appointment.AppointmentTime.AddMinutes(duration),
                Status = appointment.Status
            }, cancellationToken);

            if (result.Succeeded && !string.IsNullOrWhiteSpace(result.EventId) &&
                appointment.ExternalCalendarEventId != result.EventId)
            {
                appointment.ExternalCalendarEventId = result.EventId;
                await _db.SaveChangesAsync(cancellationToken);
            }
            else if (!result.Succeeded)
            {
                _logger.LogWarning(
                    "Calendar sync failed for appointment {AppointmentId}: {Error}",
                    appointment.Id,
                    result.Error);
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Calendar sync threw for appointment {AppointmentId}", appointment.Id);
            return CalendarSyncResult.Fail(_calendar.ProviderName, ex.Message, _calendar.IsSimulated);
        }
    }

    public async Task CancelAsync(Appointment appointment, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _calendar.CancelAppointmentAsync(appointment.ExternalCalendarEventId, cancellationToken);
            if (!result.Succeeded)
            {
                _logger.LogWarning("Calendar cancel skipped/failed: {Error}", result.Error);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Calendar cancel threw for appointment {AppointmentId}", appointment.Id);
        }
    }

    internal static string BuildEventDescription(
        string businessName,
        string status,
        string serviceName,
        int durationMinutes,
        decimal price,
        string customerName,
        string? customerPhone,
        string timeZoneId)
    {
        var lines = new List<string>
        {
            $"{businessName} appointment (REIGN)",
            $"Status: {status}",
            $"Service: {serviceName}",
            $"Duration: {durationMinutes} minutes",
            $"Price: {price:C}",
            $"Customer: {customerName}"
        };

        if (!string.IsNullOrWhiteSpace(customerPhone))
        {
            lines.Add($"Phone: {customerPhone}");
        }

        lines.Add($"Time zone: {timeZoneId}");
        lines.Add("Booked through REIGN. Do not change the time here without updating REIGN.");
        return string.Join("\n", lines);
    }

    private async Task<string> ResolveBusinessNameAsync(CancellationToken cancellationToken)
    {
        var name = await _db.Businesses.AsNoTracking()
            .Where(x => x.Active)
            .OrderBy(x => x.CreatedAt)
            .Select(x => x.Name)
            .FirstOrDefaultAsync(cancellationToken);
        return string.IsNullOrWhiteSpace(name) ? "REIGN" : name.Trim();
    }

    private async Task<string?> ResolveBusinessTimeZoneAsync(CancellationToken cancellationToken) =>
        await _db.Businesses.AsNoTracking()
            .Where(x => x.Active)
            .OrderBy(x => x.CreatedAt)
            .Select(x => x.TimeZone)
            .FirstOrDefaultAsync(cancellationToken);
}
