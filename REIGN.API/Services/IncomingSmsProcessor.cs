using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using REIGN.API.Messaging;
using REIGN.API.Options;
using REIGN.Core.AI;
using REIGN.Core.Contact;
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
            var ownNumbers = OwnDeviceNumbers();
            var resolved = PhoneNumbers.ResolveInboundEndpoints(
                incoming.From,
                incoming.To,
                incoming.ReportedPhoneNumber,
                ownNumbers);
            if (resolved.Swapped)
            {
                _logger.LogInformation(
                    "Inbound endpoints swapped; customer={Customer} device={Device} sim={Sim}",
                    resolved.From,
                    resolved.To,
                    incoming.SimNumber);
            }

            incoming.From = resolved.From;
            incoming.To = resolved.To;

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

            if (ShouldIgnoreNonCustomer(incoming, from, ownNumbers))
            {
                _logger.LogInformation("Ignored inbound from non-customer number {Phone}", from);
                return new IncomingSmsResult
                {
                    Phone = from,
                    Received = incoming.Body,
                    AutoReplied = false,
                    Intent = "ignored_non_customer"
                };
            }

            if (!string.IsNullOrWhiteSpace(_smsOptions.OwnerPhoneNumber) &&
                PhoneNumbers.AreSame(from, _smsOptions.OwnerPhoneNumber))
            {
                var ownerReply = await _ownerAssistant.AnswerAsync(incoming.Body, cancellationToken);
                _logger.LogInformation("Owner activity query handled without creating a customer thread.");
                var ownerOutbound = await TrySendAsync(from, ownerReply, sendReplyViaProvider, cancellationToken);
                return new IncomingSmsResult
                {
                    Phone = from,
                    Received = incoming.Body,
                    Reply = ownerReply,
                    AutoReplied = true,
                    OwnerNumberIgnored = true,
                    OwnerQueryHandled = true,
                    Intent = "owner_activity",
                    Outbound = ownerOutbound
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
            var state = await _state.GetOrCreate(customer.Id);
            _logger.LogInformation("Inbound {Phone} intent={Intent} status={Status}", from, intent.Label, state.CurrentStep);

            var reply = await BuildReplyAsync(customer, incoming.Body, intent);

            await _conversationService.SaveMessage(
                customer.Id,
                "Outbound",
                reply.Text,
                source: "Assistant");

            var persisted = await _db.ConversationMessages.AnyAsync(
                x => x.CustomerId == customer.Id && x.Direction == "Outbound" && x.Body == reply.Text,
                cancellationToken);

            var outbound = await TrySendAsync(from, reply.Text, sendReplyViaProvider, cancellationToken);

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
            const string fallback = "I'm having trouble on my side. Please try again in a moment.";
            var outbound = await TrySendAsync(
                PhoneNumbers.Normalize(incoming.From),
                fallback,
                sendReplyViaProvider,
                cancellationToken);
            return new IncomingSmsResult
            {
                Phone = incoming.From,
                Received = incoming.Body,
                Reply = fallback,
                AutoReplied = true,
                AiFellBack = true,
                Outbound = outbound
            };
        }
    }

    private bool ShouldIgnoreNonCustomer(
        IncomingSmsMessage incoming,
        string from,
        IReadOnlyList<string> ownNumbers)
    {
        if (!string.IsNullOrWhiteSpace(incoming.To) && PhoneNumbers.AreSame(from, incoming.To))
        {
            return true;
        }

        if (PhoneNumbers.IsOwnDeviceNumber(from, ownNumbers))
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(_smsOptions.OwnerPhoneNumber) &&
            PhoneNumbers.AreSame(from, _smsOptions.OwnerPhoneNumber))
        {
            return false;
        }

        if (incoming.SimNumber is >= 1 and <= 3 &&
            _smsOptions.SmsGate.SimNumber is >= 1 and <= 3 &&
            incoming.SimNumber != _smsOptions.SmsGate.SimNumber)
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(incoming.To) &&
            PhoneNumbers.IsOwnDeviceNumber(incoming.To, ownNumbers) &&
            !PhoneNumbers.AreSame(incoming.To, ReignContact.BusinessPhoneE164) &&
            !PhoneNumbers.AreSame(incoming.To, _smsOptions.BusinessPhoneNumber))
        {
            return true;
        }

        if (PhoneNumbers.IsShortCode(from))
        {
            return true;
        }

        return !_smsSender.IsSimulated && ReignContact.IsPlaceholder(from);
    }

    private IReadOnlyList<string> OwnDeviceNumbers() =>
        PhoneNumbers.GatewayOwnNumbers(
            _smsOptions.BusinessPhoneNumber,
            _smsOptions.SmsGate.FromNumber,
            _smsOptions.SmsGate.IgnoreFromNumbers);

    private async Task<SmsSendResult?> TrySendAsync(
        string to,
        string body,
        bool sendReplyViaProvider,
        CancellationToken cancellationToken)
    {
        if (!sendReplyViaProvider || string.IsNullOrWhiteSpace(to) || string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        var dest = PhoneNumbers.Normalize(to);
        if (PhoneNumbers.IsOwnDeviceNumber(dest, OwnDeviceNumbers()))
        {
            _logger.LogWarning("Refusing to send SMS to gateway number {Phone}", dest);
            return SmsSendResult.Fail(_smsSender.ProviderName, "Refusing to text the gateway phone.");
        }

        try
        {
            var outbound = await _smsSender.SendAsync(new SmsSendRequest
            {
                To = dest,
                Body = body
            }, cancellationToken);

            if (outbound is { Succeeded: false })
            {
                _logger.LogWarning("Outbound SMS failed for {Phone}: {Error}", to, outbound.Error);
            }

            return outbound;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Outbound SMS threw for {Phone}", to);
            return SmsSendResult.Fail(_smsSender.ProviderName, "Outbound send failed.");
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

            try
            {
                var confirmed = await _appointmentService.ConfirmAppointment(pendingAppointment.Id);
                if (confirmed?.Appointment == null)
                {
                    return new ConversationReply { Text = "I don't have a pending appointment to confirm.", Provider = "Rules" };
                }

                return new ConversationReply
                {
                    Text = $"Confirmed. Your {confirmed.Appointment.Service?.Name} is booked for {confirmed.Appointment.AppointmentTime:g} Pacific. {ReignContact.PublicName} has it on the schedule.",
                    Provider = "Rules"
                };
            }
            catch (SlotUnavailableException)
            {
                return new ConversationReply
                {
                    Text = "That time is no longer available. Please choose another day or time.",
                    Provider = "Rules"
                };
            }
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

            var cancelled = await _appointmentService.CancelAppointment(open.Id);
            return new ConversationReply
            {
                Text = $"Cancelled your {cancelled?.Service?.Name} appointment for {cancelled?.AppointmentTime:g}.",
                Provider = "Rules"
            };
        }

        if (intent.Kind == ReignIntentKind.Schedule)
        {
            var conversationState = await _state.GetOrCreate(customer.Id);
            var booking = await _bookingService.ParseRequest(message, conversationState.RequestedTime);
            if (string.IsNullOrWhiteSpace(booking.ServiceName))
            {
                booking.ServiceName = intent.ServiceName
                    ?? conversationState.SelectedService
                    ?? "";
            }

            if (!string.IsNullOrWhiteSpace(booking.ServiceName) && booking.HasTime && booking.RequestedDate != default)
            {
                conversationState.RequestedTime = booking.RequestedDate;
                await _db.SaveChangesAsync();
                try
                {
                    var write = await _appointmentService.CreateAppointment(
                        customer.Id,
                        booking.ServiceName,
                        booking.RequestedDate);

                    if (write == null)
                    {
                        return new ConversationReply { Text = "I was unable to create that appointment.", Provider = "Rules" };
                    }

                    if (write.Duplicate)
                    {
                        return new ConversationReply
                        {
                            Text = $"You already have a {booking.ServiceName} appointment scheduled for {write.Appointment.AppointmentTime:g}.",
                            Provider = "Rules"
                        };
                    }

                    if (write.Rescheduled)
                    {
                        var confirmHint = write.Appointment.Status.Equals("Confirmed", StringComparison.OrdinalIgnoreCase)
                            ? $"It's updated on {ReignContact.PublicName}'s schedule."
                            : $"Reply YES to confirm and put it on {ReignContact.PublicName}'s schedule.";
                        return new ConversationReply
                        {
                            Text = $"I updated your {booking.ServiceName} to {write.Appointment.AppointmentTime:g}. {confirmHint}",
                            Provider = "Rules"
                        };
                    }

                    return new ConversationReply
                    {
                        Text = $"Your {booking.ServiceName} for {booking.RequestedDate:g} Pacific is saved. Reply YES to confirm and put it on {ReignContact.PublicName}'s schedule.",
                        Provider = "Rules"
                    };
                }
                catch (SlotUnavailableException)
                {
                    return new ConversationReply
                    {
                        Text = "That time is not available. Please choose another day or time.",
                        Provider = "Rules"
                    };
                }
                catch (InvalidBookingException ex)
                {
                    return new ConversationReply
                    {
                        Text = ex.Message + " Please choose another day or time.",
                        Provider = "Rules"
                    };
                }
            }

            if (!string.IsNullOrWhiteSpace(booking.ServiceName) && !booking.HasTime && booking.RequestedDate != default)
            {
                conversationState.RequestedTime = booking.RequestedDate;
                conversationState.CurrentStep = "AwaitingTime";
                await _db.SaveChangesAsync();
                return new ConversationReply
                {
                    Text = $"I can schedule your {booking.ServiceName} for {booking.RequestedDate:dddd, MMM d}. What time works best?",
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
