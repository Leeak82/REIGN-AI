using Microsoft.AspNetCore.Mvc;
using REIGN.API.Calendar;
using REIGN.API.Services;
using REIGN.Data.Models;

namespace REIGN.API.Controllers;

[ApiController]
[Route("api/appointments")]
public class AppointmentActionsController : ControllerBase
{
    private readonly AppointmentService _appointments;

    public AppointmentActionsController(AppointmentService appointments)
    {
        _appointments = appointments;
    }

    [HttpPost("{id}/confirm")]
    public async Task<IActionResult> Confirm(Guid id)
    {
        try
        {
            var write = await _appointments.ConfirmAppointment(id);
            if (write?.Appointment == null)
                return NotFound();

            return Ok(CalendarResponse(
                "Appointment confirmed",
                write.Appointment,
                write.CalendarSync));
        }
        catch (SlotUnavailableException)
        {
            return Conflict(new { error = "That time is not available." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("{id}/cancel")]
    public async Task<IActionResult> Cancel(Guid id)
    {
        var appointment = await _appointments.CancelAppointment(id);
        if (appointment == null)
            return NotFound();

        return Ok(new
        {
            message = "Appointment cancelled",
            appointment.Id,
            appointment.Status
        });
    }

    [HttpPost("{id}/reschedule")]
    public async Task<IActionResult> Reschedule(Guid id, [FromBody] RescheduleRequest request)
    {
        if (request.AppointmentTime == default)
        {
            return BadRequest(new { error = "AppointmentTime is required." });
        }

        try
        {
            var write = await _appointments.UpdateAppointment(
                id,
                DateTime.SpecifyKind(request.AppointmentTime, DateTimeKind.Unspecified));
            if (write == null)
                return NotFound();

            return Ok(CalendarResponse(
                write.Duplicate ? "Appointment already at that time" : "Appointment updated",
                write.Appointment,
                write.CalendarSync));
        }
        catch (SlotUnavailableException)
        {
            return Conflict(new { error = "That time is not available." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                error = "Unable to update that appointment.",
                detail = GoogleCalendarService.SanitizeDiagnosticText(ex.Message)
            });
        }
    }

    private static object CalendarResponse(string message, Appointment appointment, CalendarSyncResult? sync) =>
        new
        {
            message,
            appointment.Id,
            appointment.Status,
            appointment.AppointmentTime,
            calendarSynced = sync?.Succeeded ?? false,
            calendarProvider = sync?.Provider,
            calendarEventId = sync is { Succeeded: true } ? sync.EventId : appointment.ExternalCalendarEventId,
            calendarSyncError = sync is { Succeeded: false } ? sync.Error : null,
            calendarHtmlLink = sync?.HtmlLink,
            calendarTimeZone = sync?.TimeZone,
            calendarId = sync?.CalendarId
        };
}

public class RescheduleRequest
{
    public DateTime AppointmentTime { get; set; }
}
