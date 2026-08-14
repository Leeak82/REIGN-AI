using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using REIGN.API.AI;
using REIGN.API.Calendar;
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
    public async Task Incoming_sms_creates_pending_qv_appointment_and_simulated_calendar_event()
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
        Assert.True(result.Outbound?.Simulated);
        Assert.True(result.Persisted);

        var appointment = await harness.Db.Appointments.Include(x => x.Service).SingleAsync();
        Assert.Equal(ServiceCatalog.QuickVisitName, appointment.Service.Name);
        Assert.Equal(150m, appointment.Price);
        Assert.Equal("Pending", appointment.Status);
        Assert.False(string.IsNullOrWhiteSpace(appointment.ExternalCalendarEventId));
        Assert.Single(harness.Calendar.Events);
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

        public static async Task<Harness> CreateAsync()
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
                OwnerPhoneNumber = "+15555550199"
            });
            var business = Options.Create(new BusinessProfileOptions());

            var sms = new SimulatedSmsSender();
            var calendar = new SimulatedCalendarService();
            var conversations = new ConversationService(db);
            var booking = new BookingService(db);
            var calendarSync = new AppointmentCalendarSync(db, calendar, NullLogger<AppointmentCalendarSync>.Instance);
            var appointments = new AppointmentService(db, calendarSync);
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
                business,
                NullLogger<ConversationEngine>.Instance);
            var ownerAssistant = new OwnerAssistantService(db, ai, business, NullLogger<OwnerAssistantService>.Instance);
            var processor = new IncomingSmsProcessor(
                conversations,
                booking,
                appointments,
                engine,
                db,
                sms,
                calendarSync,
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
