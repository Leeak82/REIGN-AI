using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using REIGN.Data;

namespace REIGN.API.Controllers;

[ApiController]
[Route("api/inbox")]
public class InboxController : ControllerBase
{
    private readonly ReignDbContext _db;

    public InboxController(ReignDbContext db)
    {
        _db = db;
    }


    [HttpGet("appointments/{customerId}")]
    public async Task<IActionResult> GetCustomerAppointments(Guid customerId)
    {
        var appointments = await _db.Appointments
            .Where(x => x.CustomerId == customerId)
            .Include(x => x.Service)
            .Select(x => new
            {
                x.Id,
                Service = x.Service.Name,
                x.AppointmentTime,
                x.Status,
                x.Price
            })
            .OrderByDescending(x => x.AppointmentTime)
            .ToListAsync();


        return Ok(appointments);
    }
}