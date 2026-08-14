using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using REIGN.API.Messaging;
using REIGN.API.Options;
using REIGN.Data;
using REIGN.Data.Models;

namespace REIGN.API.Services;

public class IncomingSmsResult
{
    public string Phone { get; set; } = "";

    public string Received { get; set; } = "";

    public string? Reply { get; set; }

    public bool AutoReplied { get; set; }

    public bool HumanOverride { get; set; }

    public bool OwnerNumberIgnored { get; set; }

    public SmsSendResult? Outbound { get; set; }
}

public class IncomingSmsProcessor
{
    private readonly ConversationService _conversationService;
    private readonly BookingService _bookingService;
    private readonly AppointmentService _appointmentService;
    private readonly ConversationEngine _engine;
    private readonly ReignDbContext _db;
    private readonly ISmsSender _smsSender;
    private readonly AppointmentCalendarSync _calendarSync;
    private readonly SmsOptions _smsOptions;
    private readonly ILogger<IncomingSmsProcessor> _logger;

    public IncomingSmsProcessor(
        ConversationService conversationService,
        BookingService bookingService,
        AppointmentService appointmentService,
        ConversationEngine engine,
        ReignDbContext db,
        ISmsSender smsSender,
        AppointmentCalendarSync calendarSync,
        IOptions<SmsOptions> smsOptions,
        ILogger<IncomingSmsProcessor> logger)
    {
        _conversationService = conversationService;
        _bookingService = bookingService;
        _appointmentService = appointmentService;
        _engine = engine;
        _db = db;
        _smsSender = smsSender;
        _calendarSync = calendarSync;
        _smsOptions = smsOptions.Value;
        _logger = logger;
    }

    public async Task<IncomingSmsResult> ProcessAsync(
        IncomingSmsMessage incoming,
        bool sendReplyViaProvider,
        CancellationToken cancellationToken = default)
    {
        var from = PhoneNumbers.Normalize(incoming.From);

        if (!string.IsNullOrWhiteSpace(_smsOptions.OwnerPhoneNumber) &&
            PhoneNumbers.AreSame(from, _smsOptions.OwnerPhoneNumber))
        {
            _logger.LogInformation("Ignored inbound SMS from the owner's personal number.");
            return new IncomingSmsResult
            {
                Phone = from,
                Received = incoming.Body,
                OwnerNumberIgnored = true,
                Reply = null
            };
        }

        var customer = await _conversationService.GetOrCreateCustomer(from, incoming.Body);

        await _conversationService.SaveMessage(
            customer.Id,
            "Inbound",
            incoming.Body,
            source: "Customer");

        if (customer.HumanOverrideActive)
        {
            return new IncomingSmsResult
            {
                Phone = from,
                Received = incoming.Body,
                HumanOverride = true,
                AutoReplied = false,
                Reply = null
            };
        }

        var reply = await BuildReplyAsync(customer, incoming.Body);

        await _conversationService.SaveMessage(
            customer.Id,
            "Outbound",
            reply,
            source: "Assistant");

        SmsSendResult? outbound = null;
        if (sendReplyViaProvider)
        {
            outbound = await _smsSender.SendAsync(new SmsSendRequest
            {
                To = from,
                Body = reply
            }, cancellationToken);
        }

        return new IncomingSmsResult
        {
            Phone = from,
            Received = incoming.Body,
            Reply = reply,
            AutoReplied = true,
            Outbound = outbound
        };
    }

    private async Task<string> BuildReplyAsync(Customer customer, string message)
    {
        if (message.Trim().Equals("YES", StringComparison.OrdinalIgnoreCase))
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
                return "I don't have a pending appointment to confirm.";
            }

            pendingAppointment.Status = "Confirmed";
            await _db.SaveChangesAsync();
            await _calendarSync.SyncAsync(pendingAppointment);
            return $"Confirmed. Your {pendingAppointment.Service.Name} appointment is booked for {pendingAppointment.AppointmentTime:g}.";
        }

        var booking = await _bookingService.ParseRequest(message);

        if (!string.IsNullOrWhiteSpace(booking.ServiceName) && booking.RequestedDate != default)
        {
            var appointment = await _appointmentService.CreateAppointment(
                customer.Id,
                booking.ServiceName,
                booking.RequestedDate);

            if (appointment == null)
            {
                return "I was unable to create that appointment.";
            }

            if (appointment.CreatedAt < DateTime.UtcNow.AddSeconds(-5))
            {
                return $"You already have a {booking.ServiceName} appointment scheduled for {appointment.AppointmentTime:g}.";
            }

            return $"Your {booking.ServiceName} appointment request for {booking.RequestedDate:g} has been saved. Reply YES to confirm.";
        }

        if (!string.IsNullOrWhiteSpace(booking.ServiceName))
        {
            return $"I can schedule your {booking.ServiceName}. What day and time works best?";
        }

        return await _engine.Process(customer, message);
    }
}
