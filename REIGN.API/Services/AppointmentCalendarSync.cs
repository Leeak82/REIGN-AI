using Microsoft.EntityFrameworkCore;
using REIGN.API.Calendar;
using REIGN.Data;
using REIGN.Data.Models;

namespace REIGN.API.Services;

public class AppointmentCalendarSync
{
    private readonly ReignDbContext _db;
    private readonly ICalendarService _calendar;
    private readonly ILogger<AppointmentCalendarSync> _logger;

    public AppointmentCalendarSync(
        ReignDbContext db,
        ICalendarService calendar,
        ILogger<AppointmentCalendarSync> logger)
    {
        _db = db;
        _calendar = calendar;
        _logger = logger;
    }

    public async Task SyncAsync(Appointment appointment, CancellationToken cancellationToken = default)
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

            var who = appointment.Customer?.Name ?? appointment.Customer?.PhoneNumber ?? "Customer";
            var serviceName = appointment.Service?.Name ?? "Appointment";
            var result = await _calendar.UpsertAppointmentAsync(new CalendarEventRequest
            {
                AppointmentId = appointment.Id,
                ExistingEventId = appointment.ExternalCalendarEventId,
                Summary = $"REIGN {serviceName} — {who}",
                Description = $"Status: {appointment.Status}\nService: {serviceName}\nPrice: {appointment.Price:C}\nCustomer: {who}",
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
                _logger.LogWarning("Calendar sync skipped/failed: {Error}", result.Error);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Calendar sync threw for appointment {AppointmentId}", appointment.Id);
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
}
