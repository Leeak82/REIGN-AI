using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using REIGN.Data;

namespace REIGN.API.Controllers;

[ApiController]
[Route("api/appointments")]
public class AppointmentsController : ControllerBase
{
    private readonly ReignDbContext _db;

    public AppointmentsController(ReignDbContext db)
    {
        _db = db;
    }


    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var appointments = await _db.Appointments
            .Include(x => x.Customer)
            .Include(x => x.Service)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new
            {
                x.Id,
                Customer = x.Customer.Name ?? "Unknown",
                Phone = x.Customer.PhoneNumber,
                Service = x.Service != null ? x.Service.Name : "Unknown",
                x.Price,
                x.AppointmentTime,
                x.Status
            })
            .ToListAsync();

        return Ok(appointments);
    }
}