using Microsoft.EntityFrameworkCore;
using REIGN.Data;
using REIGN.Data.Models;

namespace REIGN.API.Services;

public class SchedulingService
{
    private readonly ReignDbContext _db;
    private readonly AppointmentCalendarSync _calendarSync;

    public SchedulingService(ReignDbContext db, AppointmentCalendarSync calendarSync)
    {
        _db = db;
        _calendarSync = calendarSync;
    }


    public async Task<bool> IsAvailable(DateTime time)
    {
        return !await _db.Appointments
            .AnyAsync(x =>
                x.AppointmentTime == time &&
                x.Status != "Cancelled");
    }


    public async Task<Appointment?> CreateAppointment(
        Guid customerId,
        Guid serviceId,
        DateTime time)
    {
        if (!await IsAvailable(time))
            return null;


        var appointment = new Appointment
        {
            Id = Guid.NewGuid(),
            CustomerId = customerId,
            ServiceId = serviceId,
            AppointmentTime = time,
            CreatedAt = DateTime.UtcNow,
            Status = "Pending"
        };


        _db.Appointments.Add(appointment);

        await _db.SaveChangesAsync();

        await _calendarSync.SyncAsync(appointment);

        return appointment;
    }


    public async Task<List<DateTime>> GetAvailableSlots()
    {
        var slots = new List<DateTime>();

        var tomorrow = DateTime.Today.AddDays(1);

        for(int hour = 9; hour <= 16; hour++)
        {
            var slot = tomorrow.AddHours(hour);

            if(await IsAvailable(slot))
                slots.Add(slot);
        }

        return slots;
    }
}
