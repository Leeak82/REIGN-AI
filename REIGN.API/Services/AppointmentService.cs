using Microsoft.EntityFrameworkCore;
using REIGN.API.Calendar;
using REIGN.Data;
using REIGN.Data.Models;

namespace REIGN.API.Services;

public class AppointmentService
{
    private readonly ReignDbContext _db;
    private readonly AppointmentCalendarSync _calendarSync;

    public AppointmentService(ReignDbContext db, AppointmentCalendarSync calendarSync)
    {
        _db = db;
        _calendarSync = calendarSync;
    }

    public async Task<Appointment?> CreateAppointment(
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
            .FirstOrDefaultAsync(x =>
                x.CustomerId == customerId &&
                x.ServiceId == service.Id &&
                x.AppointmentTime.Date == appointmentTime.Date &&
                x.Status != "Cancelled");

        if (existing != null)
            return existing;

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
        await _calendarSync.SyncAsync(appointment);

        return appointment;
    }
}
