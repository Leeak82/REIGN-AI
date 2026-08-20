using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using REIGN.API.Messaging;
using REIGN.Data;

namespace REIGN.API.Controllers;

[ApiController]
[Route("api/customers")]
public class CustomersController : ControllerBase
{
    private readonly ReignDbContext _db;
    private readonly ILogger<CustomersController> _logger;

    public CustomersController(ReignDbContext db, ILogger<CustomersController> logger)
    {
        _db = db;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetCustomers()
    {
        var customers = await _db.Customers
            .OrderByDescending(c =>
                c.ConversationState != null
                    ? c.ConversationState.LastCustomerMessageAt ?? c.CreatedAt
                    : c.CreatedAt)
            .Select(c => new
            {
                c.Id,
                c.PhoneNumber,
                c.Name,
                c.HumanOverrideActive,
                CurrentIntent = c.ConversationState != null ? c.ConversationState.CurrentIntent : null,
                PendingServiceName = c.ConversationState != null ? c.ConversationState.SelectedService : null,
                ConversationStatus = c.ConversationState != null ? c.ConversationState.CurrentStep : null,
                MemorySummary = c.IntentMemory != null ? c.IntentMemory.Summary : null,
                TurnCount = c.ConversationState != null ? c.ConversationState.TurnCount : 0,
                Messages = c.Messages.Count,
                Appointments = c.Appointments.Count
            })
            .ToListAsync();

        return Ok(customers);
    }

    [HttpGet("{phone}")]
    public async Task<IActionResult> GetProfile(string phone)
    {
        var normalized = PhoneNumbers.Normalize(phone);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return BadRequest(new { error = "Phone is required." });
        }

        var customer = await _db.Customers
            .Include(x => x.Messages)
            .Include(x => x.Appointments)
                .ThenInclude(x => x.Service)
            .Include(x => x.ConversationState)
            .Include(x => x.IntentMemory)
            .FirstOrDefaultAsync(x => x.PhoneNumber == normalized || x.PhoneNumber == phone);

        if (customer == null)
        {
            return NotFound(new { error = "Customer not found." });
        }

        return Ok(new
        {
            customer.Id,
            customer.PhoneNumber,
            customer.Name,
            customer.Notes,
            customer.HumanOverrideActive,
            CurrentIntent = customer.ConversationState?.CurrentIntent,
            LastIntent = customer.ConversationState?.LastIntent,
            PendingServiceName = customer.ConversationState?.SelectedService,
            ConversationStatus = customer.ConversationState?.CurrentStep,
            TurnCount = customer.ConversationState?.TurnCount ?? 0,
            MemorySummary = customer.IntentMemory?.Summary,
            LastCustomerMessageAt = customer.ConversationState?.LastCustomerMessageAt,
            messages = customer.Messages
                .OrderBy(x => x.CreatedAt)
                .Select(x => new { x.Direction, x.Body, x.Source, x.CreatedAt }),
            appointments = customer.Appointments
                .OrderByDescending(x => x.AppointmentTime)
                .Select(x => new
                {
                    x.Id,
                    Service = x.Service?.Name,
                    x.AppointmentTime,
                    x.Status,
                    x.Price,
                    x.ExternalCalendarEventId
                })
        });
    }
}
