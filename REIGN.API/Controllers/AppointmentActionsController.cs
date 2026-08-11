using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using REIGN.Data;

namespace REIGN.API.Controllers;

[ApiController]
[Route("api/appointments")]
public class AppointmentActionsController : ControllerBase
{
    private readonly ReignDbContext _db;

    public AppointmentActionsController(ReignDbContext db)
    {
        _db = db;
    }

    [HttpPost("{id}/confirm")]
    public async Task<IActionResult> Confirm(Guid id)
    {
        var appointment = await _db.Appointments
            .FirstOrDefaultAsync(x => x.Id == id);

        if (appointment == null)
            return NotFound();

        appointment.Status = "Confirmed";

        await _db.SaveChangesAsync();

        return Ok(new
        {
            message = "Appointment confirmed"
        });
    }


    [HttpPost("{id}/cancel")]
    public async Task<IActionResult> Cancel(Guid id)
    {
        var appointment = await _db.Appointments
            .FirstOrDefaultAsync(x => x.Id == id);

        if (appointment == null)
            return NotFound();

        appointment.Status = "Cancelled";

        await _db.SaveChangesAsync();

        return Ok(new
        {
            message = "Appointment cancelled"
        });
    }
}
