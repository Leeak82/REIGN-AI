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
    private readonly ConversationStateService _stateService;
    private readonly IntentDetectionService _intentDetection;
    private readonly IntentMemoryService _intentMemory;
    private readonly IHttpClientFactory _httpClientFactory;

    public SMSController(
        ConversationService conversationService,
        BookingService bookingService,
        AppointmentService appointmentService,
        ReignDbContext db,
        ConversationEngine engine,
        ConversationStateService stateService,
        IntentDetectionService intentDetection,
        IntentMemoryService intentMemory,
        IHttpClientFactory httpClientFactory)
    {
        _conversationService = conversationService;
        _bookingService = bookingService;
        _appointmentService = appointmentService;
        _db = db;
        _engine = engine;
        _stateService = stateService;
        _intentDetection = intentDetection;
        _intentMemory = intentMemory;
        _httpClientFactory = httpClientFactory;
    }


    [HttpPost("incoming")]
    public async Task<IActionResult> Incoming(
        [FromBody] SMSRequest request)
    {

        var customer =
            await _conversationService
            .GetOrCreateCustomer(request.Phone);


        await _conversationService.SaveMessage(
            customer.Id,
            "Inbound",
            request.Message);


        var intent =
            _intentDetection.Detect(request.Message);


        var booking =
            await _bookingService.ParseRequest(
                request.Message);


        var activeState =
            await _stateService
            .GetActiveBookingState(customer.Id);

        if(string.IsNullOrWhiteSpace(booking.ServiceName)
            && activeState != null
            && !string.IsNullOrWhiteSpace(activeState.SelectedService))
        {
            booking.ServiceName =
                activeState.SelectedService;
        }

        if(!string.IsNullOrWhiteSpace(booking.ServiceName))
        {
            await _stateService.UpdateService(
                customer.Id,
                booking.ServiceName);
        }

        if(booking.RequestedDate != default
            && !string.IsNullOrWhiteSpace(booking.ServiceName))
        {
            await _stateService.UpdateRequestedTime(
                customer.Id,
                booking.RequestedDate);

            var appointment =
                await _appointmentService.CreateAppointment(
                    customer.Id,
                    booking.ServiceName,
                    booking.RequestedDate);

            string reply;

            if(appointment == null)
            {
                reply =
                    "I was unable to create that appointment.";
            }
            else
            {
                // Check if we have a real SMS provider configured
                bool hasRealSmsProvider =
                    !string.IsNullOrWhiteSpace(
                        _db.Set<Configuration>().FirstOrDefault(c => c.Key == "SMS:Provider")?.Value) &&
                    _db.Set<Configuration>().FirstOrDefault(c => c.Key == "SMS:Provider")?.Value?.ToLower() == "textnow";

                if (hasRealSmsProvider)
                {
                    // For real SMS providers, send a confirmation to the user's phone
                    // In a production scenario, this would send an actual SMS message
                    reply =
                        $"Your {booking.ServiceName} appointment request for {booking.RequestedDate:g} has been created. You will receive a confirmation SMS shortly.";
                }
                else
                {
                    // For simulated SMS, just inform the user
                    reply =
                        $"Your {booking.ServiceName} appointment request for {booking.RequestedDate:g} has been saved. Reply YES to confirm.";
                }
            }

            await _conversationService.SaveMessage(
                customer.Id,
                "Outbound",
                reply);

            await _intentMemory.Update(
                customer.Id,
                intent.ToString(),
                booking.ServiceName,
                "AppointmentCreated");

            return Ok(new
            {
                customer = request.Phone,
                intent,
                service = booking.ServiceName,
                reply
            });
        }

        await _intentMemory.Update(
            customer.Id,
            intent.ToString(),
            booking.ServiceName,
            "Processing");

        var response =
            await _engine.Process(
                customer,
                request.Message);

        await _conversationService.SaveMessage(
            customer.Id,
            "Outbound",
            response);

        return Ok(new
        {
            customer = request.Phone,
            intent,
            service = booking.ServiceName,
            reply = response
        });
    }
}


public class SMSRequest
{
    public string Phone { get; set; } = "";

    public string Message { get; set; } = "";
}

public class Configuration
{
    public int Id { get; set; }
    public string Key { get; set; } = "";
    public string Value { get; set; } = "";
}