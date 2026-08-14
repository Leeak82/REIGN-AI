using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using REIGN.API.Messaging;
using REIGN.API.Options;
using REIGN.Core.AI;
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

    public bool OwnerQueryHandled { get; set; }

    public string? Intent { get; set; }

    public bool AiFellBack { get; set; }

    public bool Persisted { get; set; }

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
    private readonly IntentDetectionService _intents;
    private readonly ConversationStateService _state;
    private readonly IntentMemoryService _intentMemory;
    private readonly OwnerAssistantService _ownerAssistant;
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
        IntentDetectionService intents,
        ConversationStateService state,
        IntentMemoryService intentMemory,
        OwnerAssistantService ownerAssistant,
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
        _intents = intents;
        _state = state;
        _intentMemory = intentMemory;
        _ownerAssistant = ownerAssistant;
        _smsOptions = smsOptions.Value;
        _logger = logger;
    }

    public async Task<IncomingSmsResult> ProcessAsync(
        IncomingSmsMessage incoming,
        bool sendReplyViaProvider,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var from = PhoneNumbers.Normalize(incoming.From);
            if (string.IsNullOrWhiteSpace(from) || string.IsNullOrWhiteSpace(incoming.Body))
            {
                return new IncomingSmsResult
                {
                    Phone = from,
                    Received = incoming.Body ?? "",
                    Reply = "I need a phone number and a message to help.",
                    AutoReplied = false
                };
            }

            if (!string.IsNullOrWhiteSpace(_smsOptions.OwnerPhoneNumber) &&
                PhoneNumbers.AreSame(from, _smsOptions.OwnerPhoneNumber))
            {
                var ownerReply = await _ownerAssistant.AnswerAsync(incoming.Body, cancellationToken);
                _logger.LogInformation("Owner activity query handled without creating a customer thread.");
                return new IncomingSmsResult
                {
                    Phone = from,
                    Received = incoming.Body,
                    Reply = ownerReply,
                    AutoReplied = true,
                    OwnerNumberIgnored = true,
                    OwnerQueryHandled = true,
                    Intent = "owner_activity"
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
                    Reply = null,
                    Intent = "human_override"
                };
            }

            var intent = _intents.Detect(incoming.Body, customer);
            await _state.UpdateAsync(customer, intent, incoming.Body);
            await _intentMemory.RecordAsync(customer, intent, incoming.Body);
            _logger.LogInformation("Inbound {Phone} intent={Intent} status={Status}", from, intent.Label, customer.ConversationStatus);

            var reply = await BuildReplyAsync(customer, incoming.Body, intent);

            await _conversationService.SaveMessage(
                customer.Id,
                "Outbound",
                reply.Text,
                source: "Assistant");

            var persisted = await _db.ConversationMessages.AnyAsync(
                x => x.CustomerId == customer.Id && x.Direction == "Outbound" && x.Body == reply.Text,
                cancellationToken);

            SmsSendResult? outbound = null;
            if (sendReplyViaProvider)
            {
                outbound = await _smsSender.SendAsync(new SmsSendRequest
                {
                    To = from,
                    Body = reply.Text
                }, cancellationToken);

                if (outbound is { Succeeded: false })
                {
                    _logger.LogWarning("Outbound SMS failed for {Phone}: {Error}", from, outbound.Error);
                }
            }

            return new IncomingSmsResult
            {
                Phone = from,
                Received = incoming.Body,
                Reply = reply.Text,
                AutoReplied = true,
                Outbound = outbound,
                Intent = intent.Label,
                AiFellBack = reply.FellBack,
                Persisted = persisted
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Incoming SMS processing failed");
            return new IncomingSmsResult
            {
                Phone = incoming.From,
                Received = incoming.Body,
                Reply = "I'm having trouble on my side. Please try again in a moment.",
                AutoReplied = true,
                AiFellBack = true
            };
        }
    }

    private async Task<ConversationReply> BuildReplyAsync(Customer customer, string message, DetectedIntent intent)
    {
        if (intent.Kind == ReignIntentKind.Confirm ||
            message.Trim().Equals("YES", StringComparison.OrdinalIgnoreCase))
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
                return new ConversationReply { Text = "I don't have a pending appointment to confirm.", Provider = "Rules" };
            }

            pendingAppointment.Status = "Confirmed";
            await _db.SaveChangesAsync();
            await _calendarSync.SyncAsync(pendingAppointment);
            return new ConversationReply
            {
                Text = $"Confirmed. Your {pendingAppointment.Service.Name} appointment is booked for {pendingAppointment.AppointmentTime:g}.",
                Provider = "Rules"
            };
        }

        if (intent.Kind == ReignIntentKind.Cancel)
        {
            var open = await _db.Appointments
                .Include(x => x.Service)
                .Where(x => x.CustomerId == customer.Id && x.Status != "Cancelled")
                .OrderByDescending(x => x.CreatedAt)
                .FirstOrDefaultAsync();

            if (open == null)
            {
                return new ConversationReply { Text = "I don't have an appointment to cancel.", Provider = "Rules" };
            }

            open.Status = "Cancelled";
            await _db.SaveChangesAsync();
            await _calendarSync.CancelAsync(open);
            return new ConversationReply
            {
                Text = $"Cancelled your {open.Service?.Name} appointment for {open.AppointmentTime:g}.",
                Provider = "Rules"
            };
        }

        if (intent.Kind == ReignIntentKind.Schedule)
        {
            var booking = await _bookingService.ParseRequest(message);
            if (string.IsNullOrWhiteSpace(booking.ServiceName))
            {
                booking.ServiceName = intent.ServiceName ?? customer.PendingServiceName ?? "";
            }

            if (!string.IsNullOrWhiteSpace(booking.ServiceName) && booking.RequestedDate != default)
            {
                var appointment = await _appointmentService.CreateAppointment(
                    customer.Id,
                    booking.ServiceName,
                    booking.RequestedDate);

                if (appointment == null)
                {
                    return new ConversationReply { Text = "I was unable to create that appointment.", Provider = "Rules" };
                }

                if (appointment.CreatedAt < DateTime.UtcNow.AddSeconds(-5))
                {
                    return new ConversationReply
                    {
                        Text = $"You already have a {booking.ServiceName} appointment scheduled for {appointment.AppointmentTime:g}.",
                        Provider = "Rules"
                    };
                }

                return new ConversationReply
                {
                    Text = $"Your {booking.ServiceName} appointment request for {booking.RequestedDate:g} has been saved. Reply YES to confirm.",
                    Provider = "Rules"
                };
            }

            if (!string.IsNullOrWhiteSpace(booking.ServiceName))
            {
                return new ConversationReply
                {
                    Text = $"I can schedule your {booking.ServiceName}. What day and time works best?",
                    Provider = "Rules"
                };
            }
        }

        return await _engine.ProcessDetailed(customer, message, track: false);
    }
}
