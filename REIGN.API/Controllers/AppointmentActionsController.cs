using Microsoft.AspNetCore.Mvc;
using REIGN.API.Services;

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
            var appointment = await _appointments.ConfirmAppointment(id);
            if (appointment == null)
                return NotFound();

            return Ok(new
            {
                message = "Appointment confirmed",
                appointment.Id,
                appointment.Status,
                appointment.AppointmentTime,
                calendarEventId = appointment.ExternalCalendarEventId
            });
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
            var write = await _appointments.UpdateAppointment(id, request.AppointmentTime);
            if (write == null)
                return NotFound();

            return Ok(new
            {
                message = write.Duplicate ? "Appointment already at that time" : "Appointment updated",
                write.Appointment.Id,
                write.Appointment.Status,
                write.Appointment.AppointmentTime,
                calendarEventId = write.Appointment.ExternalCalendarEventId
            });
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
}

public class RescheduleRequest
{
    public DateTime AppointmentTime { get; set; }
}
