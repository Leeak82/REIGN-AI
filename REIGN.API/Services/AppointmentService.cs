using Microsoft.EntityFrameworkCore;
using REIGN.Data;
using REIGN.Data.Models;

namespace REIGN.API.Services;

public class AppointmentService
{
    private readonly ReignDbContext _db;
    private readonly AppointmentCalendarSync _calendarSync;
    private readonly SchedulingService _scheduling;

    public AppointmentService(
        ReignDbContext db,
        AppointmentCalendarSync calendarSync,
        SchedulingService scheduling)
    {
        _db = db;
        _calendarSync = calendarSync;
        _scheduling = scheduling;
    }

    public async Task<AppointmentWriteResult?> CreateAppointment(
        Guid customerId,
        string serviceName,
        DateTime appointmentTime)
    {
        var service = await _db.Services
            .FirstOrDefaultAsync(x =>
                x.Active &&
                x.Name == serviceName);

        if (service == null)
            return null;

        var existing = await _db.Appointments
            .Include(x => x.Service)
            .Include(x => x.Customer)
            .Where(x =>
                x.CustomerId == customerId &&
                x.ServiceId == service.Id &&
                x.Status != "Cancelled")
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync();

        if (existing != null)
        {
            if (existing.AppointmentTime == appointmentTime)
            {
                return new AppointmentWriteResult
                {
                    Appointment = existing,
                    Duplicate = true
                };
            }

            return await RescheduleCore(existing, service.DurationMinutes, appointmentTime);
        }

        EnsureBookableTime(appointmentTime);

        if (!await _scheduling.IsAvailable(appointmentTime, service.DurationMinutes))
        {
            throw new SlotUnavailableException();
        }

        var appointment = new Appointment
        {
            CustomerId = customerId,
            ServiceId = service.Id,
            Price = service.Price,
            DurationMinutes = service.DurationMinutes,
            AppointmentTime = appointmentTime,
            Status = "Pending",
            CreatedAt = DateTime.UtcNow
        };

        _db.Appointments.Add(appointment);
        await _db.SaveChangesAsync();

        appointment.Service = service;
        return new AppointmentWriteResult
        {
            Appointment = appointment,
            Created = true
        };
    }

    public async Task<Appointment?> ConfirmAppointment(Guid id)
    {
        var appointment = await _db.Appointments
            .Include(x => x.Service)
            .Include(x => x.Customer)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (appointment == null)
            return null;

        if (appointment.Status.Equals("Cancelled", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("A cancelled appointment cannot be confirmed.");

        var duration = appointment.DurationMinutes > 0
            ? appointment.DurationMinutes
            : appointment.Service?.DurationMinutes ?? 30;

        if (!await _scheduling.IsAvailable(appointment.AppointmentTime, duration, appointment.Id))
        {
            throw new SlotUnavailableException();
        }

        appointment.Status = "Confirmed";
        await _db.SaveChangesAsync();
        await _calendarSync.SyncAsync(appointment);
        return appointment;
    }

    public async Task<AppointmentWriteResult?> UpdateAppointment(Guid id, DateTime appointmentTime)
    {
        var appointment = await _db.Appointments
            .Include(x => x.Service)
            .Include(x => x.Customer)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (appointment == null)
            return null;

        if (appointment.Status.Equals("Cancelled", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("A cancelled appointment cannot be updated.");

        var duration = appointment.DurationMinutes > 0
            ? appointment.DurationMinutes
            : appointment.Service?.DurationMinutes ?? 30;

        return await RescheduleCore(appointment, duration, appointmentTime);
    }

    public async Task<Appointment?> CancelAppointment(Guid id)
    {
        var appointment = await _db.Appointments
            .Include(x => x.Service)
            .Include(x => x.Customer)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (appointment == null)
            return null;

        appointment.Status = "Cancelled";
        await _db.SaveChangesAsync();
        await _calendarSync.CancelAsync(appointment);
        return appointment;
    }

    private async Task<AppointmentWriteResult> RescheduleCore(
        Appointment appointment,
        int durationMinutes,
        DateTime appointmentTime)
    {
        if (appointment.AppointmentTime == appointmentTime)
        {
            return new AppointmentWriteResult
            {
                Appointment = appointment,
                Duplicate = true
            };
        }

        EnsureBookableTime(appointmentTime);

        if (!await _scheduling.IsAvailable(appointmentTime, durationMinutes, appointment.Id))
        {
            throw new SlotUnavailableException();
        }

        appointment.AppointmentTime = appointmentTime;
        appointment.DurationMinutes = durationMinutes;
        await _db.SaveChangesAsync();

        if (appointment.Status.Equals("Confirmed", StringComparison.OrdinalIgnoreCase) ||
            !string.IsNullOrWhiteSpace(appointment.ExternalCalendarEventId))
        {
            await _calendarSync.SyncAsync(appointment);
        }

        return new AppointmentWriteResult
        {
            Appointment = appointment,
            Rescheduled = true
        };
    }

    private static void EnsureBookableTime(DateTime appointmentTime)
    {
        if (appointmentTime == default)
        {
            throw new InvalidBookingException("A day and time are required.");
        }

        if (appointmentTime < DateTime.Now.AddMinutes(-1))
        {
            throw new InvalidBookingException("That time has already passed.");
        }
    }
}
