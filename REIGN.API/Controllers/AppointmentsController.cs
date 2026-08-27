using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using REIGN.API.Calendar;
using REIGN.API.Services;
using REIGN.Core.Catalog;
using REIGN.Data;

namespace REIGN.API.Controllers;

[ApiController]
[Route("api/appointments")]
public class AppointmentsController : ControllerBase
{
    private readonly ReignDbContext _db;
    private readonly AppointmentService _appointments;
    private readonly ConversationService _conversations;
    private readonly ILogger<AppointmentsController> _logger;

    public AppointmentsController(
        ReignDbContext db,
        AppointmentService appointments,
        ConversationService conversations,
        ILogger<AppointmentsController> logger)
    {
        _db = db;
        _appointments = appointments;
        _conversations = conversations;
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

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateAppointmentRequest request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.PhoneNumber))
        {
            return BadRequest(new { error = "PhoneNumber is required." });
        }

        if (request.AppointmentTime == default)
        {
            return BadRequest(new { error = "AppointmentTime is required." });
        }

        var serviceName = ResolveServiceName(request.ServiceName, request.ServiceCode);
        if (string.IsNullOrWhiteSpace(serviceName))
        {
            return BadRequest(new { error = $"Service is required. Catalog: {ServiceCatalog.CatalogSummary}." });
        }

        try
        {
            var customer = await _conversations.GetOrCreateCustomer(request.PhoneNumber);
            if (!string.IsNullOrWhiteSpace(request.CustomerName) && string.IsNullOrWhiteSpace(customer.Name))
            {
                customer.Name = request.CustomerName.Trim();
                await _db.SaveChangesAsync();
            }

            var write = await _appointments.CreateAppointment(
                customer.Id,
                serviceName,
                DateTime.SpecifyKind(request.AppointmentTime, DateTimeKind.Unspecified));

            if (write?.Appointment == null)
            {
                return BadRequest(new { error = "I was unable to create that appointment." });
            }

            if (request.Confirm && write.Appointment.Status != "Confirmed")
            {
                write = await _appointments.ConfirmAppointment(write.Appointment.Id) ?? write;
            }

            var appointment = write.Appointment;
            return Ok(new
            {
                message = appointment.Status == "Confirmed"
                    ? (write.CalendarSync?.Succeeded == true
                        ? "Appointment booked. Jessica has it on her Google Calendar."
                        : "Appointment booked.")
                    : "Appointment saved. Confirm to add it to Jessica's calendar.",
                appointment.Id,
                customer = customer.Name ?? customer.PhoneNumber,
                phone = customer.PhoneNumber,
                service = appointment.Service?.Name ?? serviceName,
                appointment.Price,
                appointment.AppointmentTime,
                appointment.Status,
                appointment.DurationMinutes,
                calendarSynced = write.CalendarSync?.Succeeded ?? false,
                calendarProvider = write.CalendarSync?.Provider,
                calendarEventId = write.CalendarSync is { Succeeded: true }
                    ? write.CalendarSync.EventId
                    : appointment.ExternalCalendarEventId,
                calendarSyncError = write.CalendarSync is { Succeeded: false } ? write.CalendarSync.Error : null,
                calendarHtmlLink = write.CalendarSync?.HtmlLink,
                calendarTimeZone = write.CalendarSync?.TimeZone,
                calendarId = write.CalendarSync?.CalendarId
            });
        }
        catch (SlotUnavailableException)
        {
            return Conflict(new { error = "That time is not available." });
        }
        catch (InvalidBookingException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create appointment.");
            return StatusCode(500, new
            {
                error = "Unable to create that appointment.",
                detail = GoogleCalendarService.SanitizeDiagnosticText(ex.Message)
            });
        }
    }

    private static string? ResolveServiceName(string? serviceName, string? serviceCode)
    {
        var code = BookingService.MatchCatalogService(serviceCode ?? "");
        if (!string.IsNullOrWhiteSpace(code))
        {
            return code;
        }

        var named = BookingService.MatchCatalogService(serviceName ?? "");
        if (!string.IsNullOrWhiteSpace(named))
        {
            return named;
        }

        if (string.Equals(serviceName, ServiceCatalog.QuickVisitName, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(serviceName, ServiceCatalog.HalfHourName, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(serviceName, ServiceCatalog.HourName, StringComparison.OrdinalIgnoreCase))
        {
            return serviceName;
        }

        return string.IsNullOrWhiteSpace(serviceName) ? null : serviceName.Trim();
    }
}

public class CreateAppointmentRequest
{
    public string PhoneNumber { get; set; } = "";

    public string? CustomerName { get; set; }

    public string? ServiceName { get; set; }

    public string? ServiceCode { get; set; }

    public DateTime AppointmentTime { get; set; }

    public bool Confirm { get; set; } = true;
}
