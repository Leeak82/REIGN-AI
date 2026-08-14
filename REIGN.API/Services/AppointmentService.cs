using Microsoft.EntityFrameworkCore;
using REIGN.Data;
using REIGN.Data.Models;

namespace REIGN.API.Services;

public class AppointmentService
{
    private readonly ReignDbContext _db;

    public AppointmentService(ReignDbContext db)
    {
        _db = db;
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



        // Prevent duplicate customer bookings
        var existing =
            await _db.Appointments
            .FirstOrDefaultAsync(x =>
                x.CustomerId == customerId &&
                x.ServiceId == service.Id &&
                x.AppointmentTime == appointmentTime &&
                x.Status != "Cancelled");


        if (existing != null)
        {
            existing.Status = "Confirmed";

            await _db.SaveChangesAsync();

            return existing;
        }



        var appointment = new Appointment
        {
            CustomerId = customerId,
            ServiceId = service.Id,
            Price = service.Price,
            DurationMinutes = service.DurationMinutes,
            AppointmentTime = appointmentTime,
            Status = "Confirmed",
            CreatedAt = DateTime.UtcNow
        };


        _db.Appointments.Add(appointment);

        await _db.SaveChangesAsync();


        return appointment;
    }
}
