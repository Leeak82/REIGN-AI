using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using REIGN.API.Messaging;
using REIGN.Data;

namespace REIGN.API.Controllers;

[ApiController]
[Route("api/customer-appointments")]
public class CustomerAppointmentsController : ControllerBase
{
    private readonly ReignDbContext _db;

    public CustomerAppointmentsController(ReignDbContext db)
    {
        _db = db;
    }

    [HttpGet("{phone}")]
    public async Task<IActionResult> Get(string phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
        {
            return BadRequest(new { error = "Phone is required." });
        }

        var normalized = PhoneNumbers.Normalize(phone);
        var appointments = await _db.Appointments
            .Include(x => x.Customer)
            .Include(x => x.Service)
            .Where(x => x.Customer.PhoneNumber == normalized || x.Customer.PhoneNumber == phone)
            .OrderByDescending(x => x.AppointmentTime)
            .Select(x => new
            {
                x.Id,
                Service = x.Service != null ? x.Service.Name : "Unknown",
                x.AppointmentTime,
                x.Status,
                x.Price,
                x.ExternalCalendarEventId
            })
            .ToListAsync();

        return Ok(appointments);
    }
}
