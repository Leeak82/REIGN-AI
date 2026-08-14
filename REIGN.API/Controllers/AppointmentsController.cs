using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using REIGN.Data;
using REIGN.Data.Models;

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
    public async Task<IActionResult> GetAll()
    {
        var appointments = await _db.Appointments
            .Include(x => x.Customer)
            .Include(x => x.Service)
            .OrderBy(x => x.AppointmentTime)
            .Select(x => new
            {
                x.Id,
                Customer = x.Customer.Name,
                Phone = x.Customer.PhoneNumber,
                Service = x.Service.Name,
                x.AppointmentTime,
                x.Status,
                x.Price,
                x.DurationMinutes
            })
            .ToListAsync();

        return Ok(appointments);
    }


    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateAppointmentRequest request)
    {
        if (request == null)
            return BadRequest("Appointment request is required.");

        var phone = request.Phone?.Trim();

        if (string.IsNullOrWhiteSpace(phone))
            return BadRequest("Phone is required.");

        if (request.ServiceId == Guid.Empty)
            return BadRequest("ServiceId is required.");

        if (request.AppointmentTime == default)
            return BadRequest("AppointmentTime is required.");

        var service = await _db.Services
            .FirstOrDefaultAsync(x =>
                x.Id == request.ServiceId &&
                x.Active);

        if (service == null)
            return NotFound("Service was not found.");

        var customer = await _db.Customers
            .FirstOrDefaultAsync(x => x.PhoneNumber == phone);

        if (customer == null)
        {
            customer = new Customer
            {
                PhoneNumber = phone,
                CreatedAt = DateTime.UtcNow
            };

            _db.Customers.Add(customer);
        }

        if (!string.IsNullOrWhiteSpace(request.Name) &&
            string.IsNullOrWhiteSpace(customer.Name))
        {
            customer.Name = request.Name.Trim();
        }

        var appointment = new Appointment
        {
            CustomerId = customer.Id,
            ServiceId = service.Id,
            Price = service.Price,
            DurationMinutes = service.DurationMinutes,
            AppointmentTime = request.AppointmentTime,
            Notes = request.Notes,
            Status = "Pending",
            CreatedAt = DateTime.UtcNow
        };

        _db.Appointments.Add(appointment);

        await _db.SaveChangesAsync();

        return CreatedAtAction(
            nameof(GetAll),
            new { id = appointment.Id },
            new
            {
                appointment.Id,
                Customer = customer.Name,
                Phone = customer.PhoneNumber,
                Service = service.Name,
                appointment.AppointmentTime,
                appointment.Status,
                appointment.Price,
                appointment.DurationMinutes,
                appointment.Notes
            });
    }


    [HttpGet("today")]
    public async Task<IActionResult> Today()
    {
        var start = DateTime.Today;
        var end = start.AddDays(1);

        var appointments = await _db.Appointments
            .Include(x => x.Customer)
            .Include(x => x.Service)
            .Where(x =>
                x.AppointmentTime >= start &&
                x.AppointmentTime < end)
            .OrderBy(x => x.AppointmentTime)
            .ToListAsync();

        return Ok(appointments);
    }


    [HttpPut("{id}/status")]
    public async Task<IActionResult> UpdateStatus(
        Guid id,
        [FromBody] string status)
    {
        var appointment = await _db.Appointments
            .FirstOrDefaultAsync(x => x.Id == id);

        if (appointment == null)
            return NotFound();

        appointment.Status = status;

        await _db.SaveChangesAsync();

        return Ok(new
        {
            appointment.Id,
            appointment.Status
        });
    }


    public class CreateAppointmentRequest
    {
        public string Phone { get; set; } = "";

        public string? Name { get; set; }

        public Guid ServiceId { get; set; }

        public DateTime AppointmentTime { get; set; }

        public string? Notes { get; set; }
    }
}
