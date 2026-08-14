using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using REIGN.API.Services;
using REIGN.Data;

namespace REIGN.API.Controllers;

[ApiController]
[Route("api/appointments")]
public class AppointmentActionsController : ControllerBase
{
    private readonly ReignDbContext _db;
    private readonly AppointmentCalendarSync _calendarSync;

    public AppointmentActionsController(ReignDbContext db, AppointmentCalendarSync calendarSync)
    {
        _db = db;
        _calendarSync = calendarSync;
    }

    [HttpPost("{id}/confirm")]
    public async Task<IActionResult> Confirm(Guid id)
    {
        var appointment = await _db.Appointments
            .Include(x => x.Service)
            .Include(x => x.Customer)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (appointment == null)
            return NotFound();

        appointment.Status = "Confirmed";

        await _db.SaveChangesAsync();
        await _calendarSync.SyncAsync(appointment);

        return Ok(new
        {
            message = "Appointment confirmed"
        });
    }

    [HttpPost("{id}/cancel")]
    public async Task<IActionResult> Cancel(Guid id)
    {
        var appointment = await _db.Appointments
            .Include(x => x.Service)
            .Include(x => x.Customer)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (appointment == null)
            return NotFound();

        appointment.Status = "Cancelled";

        await _db.SaveChangesAsync();
        await _calendarSync.CancelAsync(appointment);

        return Ok(new
        {
            message = "Appointment cancelled"
        });
    }
}
