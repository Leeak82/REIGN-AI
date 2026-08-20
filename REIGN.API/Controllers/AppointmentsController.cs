using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using REIGN.Data;

namespace REIGN.API.Controllers;

[ApiController]
[Route("api/appointments")]
public class AppointmentsController : ControllerBase
{
    private readonly ReignDbContext _db;
    private readonly ILogger<AppointmentsController> _logger;

    public AppointmentsController(ReignDbContext db, ILogger<AppointmentsController> logger)
    {
        _db = db;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        try
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
                    x.Status,
                    x.DurationMinutes,
                    x.ExternalCalendarEventId
                })
                .ToListAsync();

            return Ok(appointments);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list appointments.");
            return StatusCode(500, new { error = "Unable to load appointments." });
        }
    }
}
