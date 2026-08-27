using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using REIGN.API.AI;
using REIGN.API.Calendar;
using REIGN.API.Controllers;
using REIGN.API.Messaging;
using REIGN.API.Options;
using REIGN.API.Services;
using REIGN.Core.AI;
using REIGN.Core.Catalog;
using REIGN.Core.Services;
using REIGN.Data;
using REIGN.Data.Schema;
using REIGN.Data.Seed;
using Xunit;

namespace REIGN.Tests;

public class IncomingSmsProcessorTests
{
    [Fact]
    public async Task Booking_maps_qv_hh_hr_not_automotive()
    {
        await using var harness = await Harness.CreateAsync();
        var booking = new BookingService(harness.Db);

        var qv = await booking.ParseRequest("I'd like a QV tomorrow at 2 pm");
        Assert.Equal(ServiceCatalog.QuickVisitName, qv.ServiceName);
        Assert.NotEqual(default, qv.RequestedDate);

        var hh = await booking.ParseRequest("half hour today 10:30 am");
        Assert.Equal(ServiceCatalog.HalfHourName, hh.ServiceName);

        var hr = await booking.ParseRequest("book an hour tomorrow 4pm");
        Assert.Equal(ServiceCatalog.HourName, hr.ServiceName);

        var oil = await booking.ParseRequest("I need an oil change tomorrow 2pm");
        Assert.True(string.IsNullOrWhiteSpace(oil.ServiceName));
    }

    [Fact]
    public async Task Booking_parses_weekdays_in_business_timezone()
    {
        await using var harness = await Harness.CreateAsync();
        var clock = new REIGN.API.Calendar.BusinessClock("America/Los_Angeles");
        var booking = new BookingService(harness.Db, clock);

        var friday = await booking.ParseRequest("Book HH Friday at 3 pm");
        Assert.Equal(ServiceCatalog.HalfHourName, friday.ServiceName);
        Assert.True(friday.HasTime);
        Assert.Equal(15, friday.RequestedDate.Hour);
        Assert.Equal(DayOfWeek.Friday, friday.RequestedDate.DayOfWeek);

        var nextMonday = await booking.ParseRequest("QV next monday 10am");
        Assert.Equal(ServiceCatalog.QuickVisitName, nextMonday.ServiceName);
        Assert.True(nextMonday.HasTime);
        Assert.Equal(DayOfWeek.Monday, nextMonday.RequestedDate.DayOfWeek);
        Assert.True(nextMonday.RequestedDate.Date > clock.Today);

        var dayOnly = await booking.ParseRequest("Half hour Saturday");
        Assert.False(dayOnly.HasTime);
        Assert.Equal(DayOfWeek.Saturday, dayOnly.RequestedDate.DayOfWeek);
    }

    [Fact]
    public async Task Incoming_sms_books_weekday_after_asking_for_time()
    {
        await using var harness = await Harness.CreateAsync();

        var asked = await harness.Processor.ProcessAsync(new IncomingSmsMessage
        {
            From = "3605550410",
            Body = "Book a half hour next Friday"
        }, sendReplyViaProvider: false);

        Assert.Contains("time", asked.Reply, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(harness.Db.Appointments);

        var timed = await harness.Processor.ProcessAsync(new IncomingSmsMessage
        {
            From = "3605550410",
            Body = "3 pm"
        }, sendReplyViaProvider: false);

        Assert.Contains("YES", timed.Reply, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Miss Reign", timed.Reply, StringComparison.OrdinalIgnoreCase);
        var appointment = Assert.Single(harness.Db.Appointments);
        Assert.Equal(15, appointment.AppointmentTime.Hour);
        Assert.Equal(DayOfWeek.Friday, appointment.AppointmentTime.DayOfWeek);
        Assert.True(appointment.AppointmentTime.Date > DateTime.UtcNow.Date.AddDays(-1));
    }

    [Fact]
    public async Task Owner_dashboard_booking_confirms_and_creates_calendar_event()
    {
        await using var harness = await Harness.CreateAsync();
        var calendarSync = new AppointmentCalendarSync(
            harness.Db,
            harness.Calendar,
            Options.Create(new GoogleCalendarOptions { TimeZone = "America/Los_Angeles" }),
            NullLogger<AppointmentCalendarSync>.Instance);
        var appointments = new AppointmentService(harness.Db, calendarSync, new SchedulingService(harness.Db));
        var controller = new AppointmentsController(
            harness.Db,
            appointments,
            harness.Conversations,
            NullLogger<AppointmentsController>.Instance);

        var when = DateTime.SpecifyKind(DateTime.UtcNow.Date.AddDays(2).AddHours(11), DateTimeKind.Unspecified);
        var result = await controller.Create(new CreateAppointmentRequest
        {
            PhoneNumber = "3605550420",
            CustomerName = "Avery",
            ServiceName = "Quick Visit",
            AppointmentTime = when,
            Confirm = true
        });

        var ok = Assert.IsType<Microsoft.AspNetCore.Mvc.OkObjectResult>(result);
        var json = System.Text.Json.JsonSerializer.Serialize(ok.Value);
        Assert.Contains("Miss Reign", json, StringComparison.Ordinal);
        Assert.Contains("Google Calendar", json, StringComparison.Ordinal);
        Assert.Single(harness.Db.Appointments);
        var appointment = harness.Db.Appointments.Include(x => x.Service).Include(x => x.Customer).Single();
        Assert.Equal("Confirmed", appointment.Status);
        Assert.Equal(ServiceCatalog.QuickVisitName, appointment.Service.Name);
        Assert.Equal("Avery", appointment.Customer.Name);
        Assert.False(string.IsNullOrWhiteSpace(appointment.ExternalCalendarEventId));
        Assert.Single(harness.Calendar.Events);
        Assert.NotNull(ok.Value);
    }

    [Fact]
    public async Task Incoming_sms_creates_pending_qv_appointment_without_calendar_until_confirm()
    {
        await using var harness = await Harness.CreateAsync();

        var result = await harness.Processor.ProcessAsync(new IncomingSmsMessage
        {
            From = "3605550100",
            To = "+15555550100",
            Body = "Book a quick visit tomorrow at 2 pm",
            Provider = "Internal"
        }, sendReplyViaProvider: true);

        Assert.True(result.AutoReplied);
        Assert.Contains("Quick Visit", result.Reply, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("YES", result.Reply, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Miss Reign", result.Reply, StringComparison.OrdinalIgnoreCase);
        Assert.True(result.Outbound?.Simulated);
        Assert.True(result.Persisted);

        var appointment = await harness.Db.Appointments.Include(x => x.Service).SingleAsync();
        Assert.Equal(ServiceCatalog.QuickVisitName, appointment.Service.Name);
        Assert.Equal(150m, appointment.Price);
        Assert.Equal("Pending", appointment.Status);
        Assert.True(string.IsNullOrWhiteSpace(appointment.ExternalCalendarEventId));
        Assert.Empty(harness.Calendar.Events);

        var confirmed = await harness.Processor.ProcessAsync(new IncomingSmsMessage
        {
            From = "3605550100",
            Body = "YES",
            Provider = "Internal"
        }, sendReplyViaProvider: false);

        Assert.Contains("Confirmed", confirmed.Reply, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Miss Reign", confirmed.Reply, StringComparison.OrdinalIgnoreCase);
        appointment = await harness.Db.Appointments.SingleAsync();
        Assert.Equal("Confirmed", appointment.Status);
        Assert.False(string.IsNullOrWhiteSpace(appointment.ExternalCalendarEventId));
        Assert.Single(harness.Calendar.Events);
    }

    [Fact]
    public async Task Overlapping_slot_is_rejected()
    {
        await using var harness = await Harness.CreateAsync();

        await harness.Processor.ProcessAsync(new IncomingSmsMessage
        {
            From = "3605550101",
            Body = "Book HH tomorrow at 2 pm"
        }, sendReplyViaProvider: false);

        var overlap = await harness.Processor.ProcessAsync(new IncomingSmsMessage
        {
            From = "3605550102",
            Body = "Book QV tomorrow at 2:15 pm"
        }, sendReplyViaProvider: false);

        Assert.Contains("not available", overlap.Reply, StringComparison.OrdinalIgnoreCase);
        Assert.Single(harness.Db.Appointments);
    }

    [Fact]
    public async Task Reschedule_and_cancel_update_calendar_after_confirm()
    {
        await using var harness = await Harness.CreateAsync();

        await harness.Processor.ProcessAsync(new IncomingSmsMessage
        {
            From = "3605550103",
            Body = "Book HR tomorrow at 10 am"
        }, sendReplyViaProvider: false);

        await harness.Processor.ProcessAsync(new IncomingSmsMessage
        {
            From = "3605550103",
            Body = "YES"
        }, sendReplyViaProvider: false);

        var moved = await harness.Processor.ProcessAsync(new IncomingSmsMessage
        {
            From = "3605550103",
            Body = "Book HR tomorrow at 3 pm"
        }, sendReplyViaProvider: false);

        Assert.Contains("updated", moved.Reply, StringComparison.OrdinalIgnoreCase);
        var appointment = await harness.Db.Appointments.SingleAsync();
        Assert.Equal(15, appointment.AppointmentTime.Hour);
        Assert.Equal("Confirmed", appointment.Status);
        Assert.Single(harness.Calendar.Events);
        Assert.Equal(appointment.ExternalCalendarEventId, harness.Calendar.Events.Single().EventId);
        Assert.Equal(appointment.AppointmentTime, harness.Calendar.Events.Single().Start);

        var cancelled = await harness.Processor.ProcessAsync(new IncomingSmsMessage
        {
            From = "3605550103",
            Body = "cancel my appointment"
        }, sendReplyViaProvider: false);

        Assert.Contains("Cancelled", cancelled.Reply, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("Cancelled", (await harness.Db.Appointments.SingleAsync()).Status);
        Assert.True(harness.Calendar.Events.Single().Cancelled);
    }

    [Fact]
    public async Task Owner_override_pauses_auto_reply_until_resume()
    {
        await using var harness = await Harness.CreateAsync();

        await harness.Processor.ProcessAsync(new IncomingSmsMessage
        {
            From = "3605550100",
            Body = "Hi my name is Alex",
            Provider = "Internal"
        }, sendReplyViaProvider: false);

        var owner = new OwnerMessagingService(harness.Db, harness.Conversations, harness.Sms);
        var send = await owner.SendOverrideAsync("+13605550100", "This is Miss Reign, I'll take it from here.");
        Assert.True(send.Succeeded);
        Assert.True(send.HumanOverrideActive);

        var paused = await harness.Processor.ProcessAsync(new IncomingSmsMessage
        {
            From = "3605550100",
            Body = "Book HR tomorrow 3pm",
            Provider = "Internal"
        }, sendReplyViaProvider: true);

        Assert.True(paused.HumanOverride);
        Assert.False(paused.AutoReplied);
        Assert.Null(paused.Reply);

        await owner.ResumeAssistantAsync("+13605550100");

        var resumed = await harness.Processor.ProcessAsync(new IncomingSmsMessage
        {
            From = "3605550100",
            Body = "Book HR tomorrow 3pm",
            Provider = "Internal"
        }, sendReplyViaProvider: false);

        Assert.True(resumed.AutoReplied);
        Assert.Contains("Hour", resumed.Reply, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Owner_personal_number_is_not_treated_as_a_customer()
    {
        await using var harness = await Harness.CreateAsync();
        var result = await harness.Processor.ProcessAsync(new IncomingSmsMessage
        {
            From = "+15555550199",
            Body = "What's happening today?",
            Provider = "Internal"
        }, sendReplyViaProvider: false);

        Assert.True(result.OwnerNumberIgnored);
        Assert.True(result.OwnerQueryHandled);
        Assert.False(string.IsNullOrWhiteSpace(result.Reply));
        Assert.Empty(harness.Db.Customers);
    }

    [Fact]
    public async Task Carrier_short_codes_do_not_get_a_reply()
    {
        await using var harness = await Harness.CreateAsync();
        var result = await harness.Processor.ProcessAsync(new IncomingSmsMessage
        {
            From = "611611",
            Body = "Your Verizon account...",
            Provider = "SmsGate"
        }, sendReplyViaProvider: true);

        Assert.Equal("ignored_non_customer", result.Intent);
        Assert.False(result.AutoReplied);
        Assert.Null(result.Reply);
        Assert.Empty(harness.Sms.Sent);
        Assert.Empty(harness.Db.Customers);
    }

    [Fact]
    public async Task Gateway_own_sims_do_not_get_a_reply()
    {
        await using var harness = await Harness.CreateAsync(ignoreFrom: "+19072132242", simNumber: 1);

        var fromBusiness = await harness.Processor.ProcessAsync(new IncomingSmsMessage
        {
            From = "+19073001244",
            To = "+19072132242",
            Body = "Hi Miss Reign",
            Provider = "SmsGate",
            SimNumber = 2
        }, sendReplyViaProvider: true);
        Assert.Equal("ignored_non_customer", fromBusiness.Intent);
        Assert.Empty(harness.Sms.Sent);

        var fromCompanion = await harness.Processor.ProcessAsync(new IncomingSmsMessage
        {
            From = "+19072132242",
            To = "+19073001244",
            Body = "Hi from the other SIM",
            Provider = "SmsGate",
            SimNumber = 1
        }, sendReplyViaProvider: true);
        Assert.Equal("ignored_non_customer", fromCompanion.Intent);

        var onCompanionSim = await harness.Processor.ProcessAsync(new IncomingSmsMessage
        {
            From = "+13609261856",
            To = "+19072132242",
            Body = "Book QV",
            Provider = "SmsGate",
            SimNumber = 2
        }, sendReplyViaProvider: true);
        Assert.Equal("ignored_non_customer", onCompanionSim.Intent);
        Assert.Empty(harness.Sms.Sent);
        Assert.Empty(harness.Db.Customers);
    }

    [Fact]
    public async Task SkipCalls_inbox_is_not_treated_as_a_companion_sim()
    {
        await using var harness = await Harness.CreateAsync(skipCallsFrom: "+18136380375");

        var result = await harness.Processor.ProcessAsync(new IncomingSmsMessage
        {
            From = "+12538319100",
            To = "+18136380375",
            Body = "Hi Miss Reign",
            Provider = "SkipCalls"
        }, sendReplyViaProvider: true);

        Assert.True(result.AutoReplied);
        Assert.NotEqual("ignored_non_customer", result.Intent);
        Assert.Equal("+12538319100", result.Phone);
        Assert.Equal("+12538319100", Assert.Single(harness.Sms.Sent).To);
    }

    [Fact]
    public async Task Swapped_gateway_endpoints_reply_to_the_customer_handset()
    {
        await using var harness = await Harness.CreateAsync(ignoreFrom: "+19072132242", simNumber: 1);

        var result = await harness.Processor.ProcessAsync(new IncomingSmsMessage
        {
            From = "+19073001244",
            To = "+13609261856",
            Body = "Hi Miss Reign",
            Provider = "SmsGate",
            SimNumber = 1
        }, sendReplyViaProvider: true);

        Assert.True(result.AutoReplied);
        Assert.Equal("+13609261856", result.Phone);
        Assert.False(string.IsNullOrWhiteSpace(result.Reply));
        var sent = Assert.Single(harness.Sms.Sent);
        Assert.Equal("+13609261856", sent.To);
        Assert.DoesNotContain("+19073001244", harness.Sms.Sent.Select(x => x.To));
        Assert.DoesNotContain("+19072132242", harness.Sms.Sent.Select(x => x.To));
        Assert.Equal("+13609261856", Assert.Single(harness.Db.Customers).PhoneNumber);
    }

    [Fact]
    public async Task Legacy_phoneNumber_customer_is_used_when_sender_is_the_gateway()
    {
        await using var harness = await Harness.CreateAsync(ignoreFrom: "+19072132242", simNumber: 1);

        var result = await harness.Processor.ProcessAsync(new IncomingSmsMessage
        {
            From = "+19073001244",
            To = "+19073001244",
            ReportedPhoneNumber = "+13609261856",
            Body = "Book QV",
            Provider = "SmsGate",
            SimNumber = 1
        }, sendReplyViaProvider: true);

        Assert.True(result.AutoReplied);
        Assert.Equal("+13609261856", result.Phone);
        Assert.Equal("+13609261856", Assert.Single(harness.Sms.Sent).To);
    }

    internal sealed class Harness : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;

        public ReignDbContext Db { get; }
        public IncomingSmsProcessor Processor { get; }
        public ConversationService Conversations { get; }
        public SimulatedSmsSender Sms { get; }
        public SimulatedCalendarService Calendar { get; }
        public OwnerAssistantService OwnerAssistant { get; }

        private Harness(
            SqliteConnection connection,
            ReignDbContext db,
            IncomingSmsProcessor processor,
            ConversationService conversations,
            SimulatedSmsSender sms,
            SimulatedCalendarService calendar,
            OwnerAssistantService ownerAssistant)
        {
            _connection = connection;
            Db = db;
            Processor = processor;
            Conversations = conversations;
            Sms = sms;
            Calendar = calendar;
            OwnerAssistant = ownerAssistant;
        }

        public static async Task<Harness> CreateAsync(
            string? ignoreFrom = null,
            int simNumber = 0,
            string? skipCallsFrom = null)
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();

            var options = new DbContextOptionsBuilder<ReignDbContext>()
                .UseSqlite(connection)
                .Options;

            var db = new ReignDbContext(options);
            await SqliteSchemaUpgrades.ApplyAsync(db);
            await ServiceCatalogBootstrapper.EnsureAsync(db);

            var smsOptions = Options.Create(new SmsOptions
            {
                Provider = "Simulated",
                BusinessPhoneNumber = "+15555550100",
                OwnerPhoneNumber = "+15555550199",
                SmsGate = new SmsGateOptions
                {
                    IgnoreFromNumbers = ignoreFrom ?? "",
                    SimNumber = simNumber
                },
                SkipCalls = new SkipCallsOptions
                {
                    FromNumber = skipCallsFrom ?? ""
                }
            });
            var profiles = new BusinessProfileService(db);

            var sms = new SimulatedSmsSender();
            var calendar = new SimulatedCalendarService();
            var conversations = new ConversationService(db);
            var booking = new BookingService(db);
            var calendarSync = new AppointmentCalendarSync(
                db,
                calendar,
                Options.Create(new GoogleCalendarOptions()),
                NullLogger<AppointmentCalendarSync>.Instance);
            var scheduling = new SchedulingService(db);
            var appointments = new AppointmentService(db, calendarSync, scheduling);
            var intents = new IntentDetectionService();
            var state = new ConversationStateService(db);
            var intentMemory = new IntentMemoryService(db);
            var customerMemory = new CustomerMemoryService(db);
            IAiProvider ai = new FallbackAiProvider(new ConversationAIService(), new ReignAssistant());
            var engine = new ConversationEngine(
                db,
                intents,
                state,
                customerMemory,
                intentMemory,
                ai,
                profiles,
                NullLogger<ConversationEngine>.Instance);
            var ownerAssistant = new OwnerAssistantService(db, ai, profiles, NullLogger<OwnerAssistantService>.Instance);
            var processor = new IncomingSmsProcessor(
                conversations,
                booking,
                appointments,
                engine,
                db,
                sms,
                intents,
                state,
                intentMemory,
                ownerAssistant,
                smsOptions,
                NullLogger<IncomingSmsProcessor>.Instance);

            return new Harness(connection, db, processor, conversations, sms, calendar, ownerAssistant);
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }
}
