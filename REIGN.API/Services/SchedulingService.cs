using Microsoft.EntityFrameworkCore;
using REIGN.Data;
using REIGN.Data.Models;

namespace REIGN.API.Services;

public class SchedulingService
{
    private readonly ReignDbContext _db;

    public SchedulingService(ReignDbContext db)
    {
        _db = db;
    }

    public async Task<bool> IsAvailable(
        DateTime time,
        int durationMinutes = 30,
        Guid? excludingAppointmentId = null)
    {
        var duration = Math.Max(durationMinutes, 1);
        var start = time;
        var end = time.AddMinutes(duration);
        var dayStart = time.Date;
        var dayEnd = dayStart.AddDays(1);

        var sameDay = await _db.Appointments
            .Where(x =>
                x.Status != "Cancelled" &&
                x.AppointmentTime >= dayStart &&
                x.AppointmentTime < dayEnd &&
                (excludingAppointmentId == null || x.Id != excludingAppointmentId.Value))
            .Select(x => new { x.AppointmentTime, x.DurationMinutes })
            .ToListAsync();

        return !sameDay.Any(x =>
        {
            var otherDuration = x.DurationMinutes > 0 ? x.DurationMinutes : 30;
            var otherStart = x.AppointmentTime;
            var otherEnd = x.AppointmentTime.AddMinutes(otherDuration);
            return start < otherEnd && end > otherStart;
        });
    }

    public async Task<Appointment?> CreateAppointment(
        Guid customerId,
        Guid serviceId,
        DateTime time)
    {
        var service = await _db.Services.FirstOrDefaultAsync(x => x.Id == serviceId);
        var duration = service?.DurationMinutes ?? 30;
        if (!await IsAvailable(time, duration))
            return null;

        var appointment = new Appointment
        {
            Id = Guid.NewGuid(),
            CustomerId = customerId,
            ServiceId = serviceId,
            Price = service?.Price ?? 0,
            DurationMinutes = duration,
            AppointmentTime = time,
            CreatedAt = DateTime.UtcNow,
            Status = "Pending"
        };

        _db.Appointments.Add(appointment);
        await _db.SaveChangesAsync();
        return appointment;
    }

    public async Task<List<DateTime>> GetAvailableSlots(int durationMinutes = 30)
    {
        var slots = new List<DateTime>();
        var tomorrow = DateTime.Today.AddDays(1);

        for (int hour = 9; hour <= 16; hour++)
        {
            var slot = tomorrow.AddHours(hour);
            if (await IsAvailable(slot, durationMinutes))
                slots.Add(slot);
        }

        return slots;
    }
}
