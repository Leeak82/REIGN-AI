using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using REIGN.API.Messaging;
using REIGN.API.Services;
using REIGN.Data;
using REIGN.Data.Models;

namespace REIGN.API.Controllers;

[ApiController]
[Route("api/customers")]
public class CustomersController : ControllerBase
{
    private readonly ReignDbContext _db;
    private readonly AppointmentCalendarSync _calendarSync;
    private readonly IConfiguration _configuration;
    private readonly ILogger<CustomersController> _logger;

    public CustomersController(
        ReignDbContext db,
        AppointmentCalendarSync calendarSync,
        IConfiguration configuration,
        ILogger<CustomersController> logger)
    {
        _db = db;
        _calendarSync = calendarSync;
        _configuration = configuration;
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
                Appointments = c.Appointments.Count,
                SimulationMessages = c.Messages.Count(x =>
                    x.Source == "SimulationCustomer" ||
                    x.Source == "SimulationAssistant")
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
            SimulationMessages = customer.Messages.Count(x =>
                x.Source == "SimulationCustomer" ||
                x.Source == "SimulationAssistant"),
            messages = customer.Messages
                .OrderBy(x => x.CreatedAt)
                .Select(x => new { x.Id, x.Direction, x.Body, x.Source, x.IsOwnerOverride, x.CreatedAt }),
            appointments = customer.Appointments
                .OrderByDescending(x => x.AppointmentTime)
                .Select(x => new
                {
                    x.Id,
                    Service = x.Service != null ? x.Service.Name : "",
                    Customer = customer.Name ?? customer.PhoneNumber,
                    Phone = customer.PhoneNumber,
                    x.AppointmentTime,
                    x.Status,
                    x.Price,
                    x.DurationMinutes,
                    x.ExternalCalendarEventId
                })
        });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CustomerEditRequest? request)
    {
        var auth = RequireAdmin();
        if (auth != null)
        {
            return auth;
        }

        var validation = Validate(request, out var phone);
        if (validation != null)
        {
            return BadRequest(new { error = validation });
        }

        if (await _db.Customers.AnyAsync(x => x.PhoneNumber == phone))
        {
            return Conflict(new { error = "A customer with that phone number already exists." });
        }

        var businessId = await _db.Businesses
            .Where(x => x.Active)
            .OrderBy(x => x.CreatedAt)
            .Select(x => (Guid?)x.Id)
            .FirstOrDefaultAsync();

        var customer = new Customer
        {
            BusinessId = businessId,
            PhoneNumber = phone,
            Name = Clean(request!.Name),
            Notes = Clean(request.Notes),
            CreatedAt = DateTime.UtcNow
        };

        _db.Customers.Add(customer);
        await _db.SaveChangesAsync();
        return Ok(ToAdminDto(customer));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] CustomerEditRequest? request)
    {
        var auth = RequireAdmin();
        if (auth != null)
        {
            return auth;
        }

        var validation = Validate(request, out var phone);
        if (validation != null)
        {
            return BadRequest(new { error = validation });
        }

        var customer = await _db.Customers
            .Include(x => x.Appointments)
                .ThenInclude(x => x.Service)
            .FirstOrDefaultAsync(x => x.Id == id);
        if (customer == null)
        {
            return NotFound(new { error = "Customer not found." });
        }

        if (await _db.Customers.AnyAsync(x => x.Id != id && x.PhoneNumber == phone))
        {
            return Conflict(new { error = "Another customer already uses that phone number." });
        }

        customer.PhoneNumber = phone;
        customer.Name = Clean(request!.Name);
        customer.Notes = Clean(request.Notes);
        await _db.SaveChangesAsync();

        var calendarFailures = new List<string>();
        foreach (var appointment in customer.Appointments.Where(x =>
                     x.Status.Equals("Confirmed", StringComparison.OrdinalIgnoreCase) ||
                     !string.IsNullOrWhiteSpace(x.ExternalCalendarEventId)))
        {
            var sync = await _calendarSync.SyncAsync(appointment);
            if (!sync.Succeeded)
            {
                calendarFailures.Add(appointment.Id.ToString());
            }
        }

        return Ok(new
        {
            customer.Id,
            customer.PhoneNumber,
            customer.Name,
            customer.Notes,
            Warning = calendarFailures.Count == 0
                ? null
                : "Contact information was saved, but one or more linked calendar events could not be refreshed."
        });
    }

    [HttpPost("{id:guid}/delete")]
    public async Task<IActionResult> Delete(Guid id, [FromBody] CustomerDeleteRequest? request)
    {
        var auth = RequireAdmin();
        if (auth != null)
        {
            return auth;
        }

        var customer = await _db.Customers
            .Include(x => x.Messages)
            .Include(x => x.Appointments)
                .ThenInclude(x => x.Service)
            .Include(x => x.ConversationState)
            .Include(x => x.IntentMemory)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (customer == null)
        {
            return NotFound(new { error = "Customer not found." });
        }

        var confirmation = PhoneNumbers.Normalize(request?.ConfirmPhone);
        if (string.IsNullOrWhiteSpace(confirmation) ||
            !PhoneNumbers.AreSame(confirmation, customer.PhoneNumber))
        {
            return BadRequest(new { error = "Enter the customer's current phone number to confirm deletion." });
        }

        if (customer.Appointments.Count > 0 && request?.DeleteAppointments != true)
        {
            return Conflict(new
            {
                error = "This customer has appointments. Check 'cancel and remove appointments too' before deleting the customer."
            });
        }

        if (request?.DeleteAppointments == true)
        {
            foreach (var appointment in customer.Appointments.ToList())
            {
                if (!string.IsNullOrWhiteSpace(appointment.ExternalCalendarEventId))
                {
                    var cancelled = await _calendarSync.CancelWithResultAsync(appointment);
                    if (!cancelled.Succeeded)
                    {
                        return Conflict(new
                        {
                            error = "REIGN could not verify cancellation of a linked calendar event, so the customer was not deleted. Try again after checking Calendar/Integrations."
                        });
                    }
                }
            }

            _db.Appointments.RemoveRange(customer.Appointments);
        }

        _db.ConversationMessages.RemoveRange(customer.Messages);
        if (customer.ConversationState != null)
        {
            _db.ConversationStates.Remove(customer.ConversationState);
        }
        if (customer.IntentMemory != null)
        {
            _db.CustomerIntentMemories.Remove(customer.IntentMemory);
        }
        _db.Customers.Remove(customer);
        await _db.SaveChangesAsync();

        _logger.LogInformation("Admin deleted customer record {CustomerId}", id);
        return Ok(new { deleted = true });
    }

    [HttpPost("purge-simulations")]
    public async Task<IActionResult> PurgeSimulations()
    {
        var auth = RequireAdmin();
        if (auth != null)
        {
            return auth;
        }

        var simulatedMessages = await _db.ConversationMessages
            .Where(x => x.Source == "SimulationCustomer" || x.Source == "SimulationAssistant")
            .ToListAsync();

        if (simulatedMessages.Count == 0)
        {
            return Ok(new { messagesDeleted = 0, customersDeleted = 0 });
        }

        var customerIds = simulatedMessages.Select(x => x.CustomerId).Distinct().ToList();
        _db.ConversationMessages.RemoveRange(simulatedMessages);
        await _db.SaveChangesAsync();

        var candidateCustomers = await _db.Customers
            .Include(x => x.Messages)
            .Include(x => x.Appointments)
            .Include(x => x.ConversationState)
            .Include(x => x.IntentMemory)
            .Where(x => customerIds.Contains(x.Id))
            .ToListAsync();

        var emptySimulationCustomers = candidateCustomers
            .Where(x => x.Messages.Count == 0 && x.Appointments.Count == 0)
            .ToList();

        foreach (var customer in emptySimulationCustomers)
        {
            if (customer.ConversationState != null)
            {
                _db.ConversationStates.Remove(customer.ConversationState);
            }
            if (customer.IntentMemory != null)
            {
                _db.CustomerIntentMemories.Remove(customer.IntentMemory);
            }
        }
        _db.Customers.RemoveRange(emptySimulationCustomers);
        await _db.SaveChangesAsync();

        _logger.LogInformation(
            "Admin purged {MessageCount} tagged simulation messages and {CustomerCount} empty simulation customers",
            simulatedMessages.Count,
            emptySimulationCustomers.Count);

        return Ok(new
        {
            messagesDeleted = simulatedMessages.Count,
            customersDeleted = emptySimulationCustomers.Count
        });
    }

    private IActionResult? RequireAdmin()
    {
        var configured = _configuration["REIGN_ADMIN_API_KEY"];
        if (string.IsNullOrWhiteSpace(configured))
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = "Customer management is not configured." });
        }

        var supplied = Request.Headers["X-REIGN-ADMIN-KEY"].ToString();
        var left = Encoding.UTF8.GetBytes(configured);
        var right = Encoding.UTF8.GetBytes(supplied ?? "");
        if (left.Length != right.Length || !CryptographicOperations.FixedTimeEquals(left, right))
        {
            return Unauthorized(new { error = "Admin authorization is required." });
        }

        return null;
    }

    private static string? Validate(CustomerEditRequest? request, out string phone)
    {
        phone = PhoneNumbers.Normalize(request?.PhoneNumber);
        if (string.IsNullOrWhiteSpace(phone) || PhoneNumbers.IsShortCode(phone))
        {
            return "A valid customer phone number is required.";
        }

        if ((request?.Name?.Trim().Length ?? 0) > 120)
        {
            return "Customer name must be 120 characters or fewer.";
        }

        if ((request?.Notes?.Length ?? 0) > 2000)
        {
            return "Notes must be 2,000 characters or fewer.";
        }

        return null;
    }

    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static object ToAdminDto(Customer customer) => new
    {
        customer.Id,
        customer.PhoneNumber,
        customer.Name,
        customer.Notes
    };
}

public class CustomerEditRequest
{
    public string PhoneNumber { get; set; } = "";

    public string? Name { get; set; }

    public string? Notes { get; set; }
}

public class CustomerDeleteRequest
{
    public string ConfirmPhone { get; set; } = "";

    public bool DeleteAppointments { get; set; }
}
