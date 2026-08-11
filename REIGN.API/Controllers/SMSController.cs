using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using REIGN.API.Services;
using REIGN.Data;

namespace REIGN.API.Controllers;

[ApiController]
[Route("api/sms")]
public class SMSController : ControllerBase
{
    private readonly ConversationService _conversationService;
    private readonly BookingService _bookingService;
    private readonly AppointmentService _appointmentService;
    private readonly ReignDbContext _db;
    private readonly ConversationEngine _engine;

    public SMSController(
        ConversationService conversationService,
        BookingService bookingService,
        AppointmentService appointmentService,
        ReignDbContext db,
        ConversationEngine engine)
    {
        _conversationService = conversationService;
        _bookingService = bookingService;
        _appointmentService = appointmentService;
        _db = db;
        _engine = engine;
    }


    [HttpPost("incoming")]
    public async Task<IActionResult> Incoming([FromBody] SMSRequest request)
    {
        var customer = await _conversationService
            .GetOrCreateCustomer(request.Phone);


        await _conversationService.SaveMessage(
            customer.Id,
            "Inbound",
            request.Message);


        string reply;


        if (request.Message.Trim()
            .Equals("YES", StringComparison.OrdinalIgnoreCase))
        {
            var pendingAppointment = await _db.Appointments
                .Include(x => x.Service)
                .Where(x =>
                    x.CustomerId == customer.Id &&
                    x.Status == "Pending")
                .OrderByDescending(x => x.CreatedAt)
                .FirstOrDefaultAsync();


            if (pendingAppointment == null)
            {
                reply = "I don't have a pending appointment to confirm.";
            }
            else
            {
                pendingAppointment.Status = "Confirmed";

                await _db.SaveChangesAsync();

                reply =
                    $"Confirmed. Your {pendingAppointment.Service.Name} appointment is booked for {pendingAppointment.AppointmentTime:g}.";
            }
        }
        else
        {
            var booking = await _bookingService
                .ParseRequest(request.Message);


            if (!string.IsNullOrWhiteSpace(booking.ServiceName)
                && booking.RequestedDate != default)
            {
                var appointment = await _appointmentService.CreateAppointment(
                    customer.Id,
                    booking.ServiceName,
                    booking.RequestedDate);


                if (appointment == null)
                {
                    reply = "I was unable to create that appointment.";
                }
                else if (appointment.CreatedAt < DateTime.UtcNow.AddSeconds(-5))
                {
                    reply =
                        $"You already have a {booking.ServiceName} appointment scheduled for {appointment.AppointmentTime:g}.";
                }
                else
                {
                    reply =
                        $"Your {booking.ServiceName} appointment request for {booking.RequestedDate:g} has been saved. Reply YES to confirm.";
                }
            }
            else if (!string.IsNullOrWhiteSpace(booking.ServiceName))
            {
                reply =
                    $"I can schedule your {booking.ServiceName}. What day and time works best?";
            }
            else
            {
                reply = await _engine.Process(
                    customer,
                    request.Message);
            }
        }


        await _conversationService.SaveMessage(
            customer.Id,
            "Outbound",
            reply);


        return Ok(new
        {
            customer = request.Phone,
            received = request.Message,
            reply
        });
    }
}


public class SMSRequest
{
    public string Phone { get; set; } = "";

    public string Message { get; set; } = "";
}