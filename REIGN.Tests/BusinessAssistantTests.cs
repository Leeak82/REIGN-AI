using Microsoft.EntityFrameworkCore;
using REIGN.API.Messaging;
using REIGN.API.Services;
using REIGN.Core.AI;
using REIGN.Core.Catalog;
using Xunit;

namespace REIGN.Tests;

public class BusinessAssistantTests
{
    [Fact]
    public void Reschedule_language_is_scheduling_intent()
    {
        var intents = new IntentDetectionService();
        var detected = intents.Detect("Can you reschedule my half hour to tomorrow at 4 pm?");
        Assert.Equal(ReignIntentKind.Schedule, detected.Kind);
        Assert.Equal(ServiceCatalog.HalfHourName, detected.ServiceName);
    }

    [Fact]
    public async Task Catalog_recommends_quick_visit_from_qv_language()
    {
        await using var harness = await IncomingSmsProcessorTests.Harness.CreateAsync();
        var catalog = new CatalogIntelligence(harness.Db);
        var result = await catalog.RecommendAsync("How much is a QV?");
        Assert.Equal(ServiceCatalog.QuickVisitName, result.Service);
        Assert.Equal(150m, result.Price);
    }

    [Fact]
    public async Task Past_appointment_time_is_rejected()
    {
        await using var harness = await IncomingSmsProcessorTests.Harness.CreateAsync();
        await harness.Processor.ProcessAsync(new IncomingSmsMessage
        {
            From = "3605550301",
            Body = "Hi my name is Sam"
        }, sendReplyViaProvider: false);

        var customer = await harness.Db.Customers.SingleAsync();
        var calendarSync = new AppointmentCalendarSync(
            harness.Db,
            harness.Calendar,
            Microsoft.Extensions.Options.Options.Create(new REIGN.API.Options.GoogleCalendarOptions()),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<AppointmentCalendarSync>.Instance);
        var appointments = new AppointmentService(harness.Db, calendarSync, new SchedulingService(harness.Db));

        await Assert.ThrowsAsync<InvalidBookingException>(() =>
            appointments.CreateAppointment(customer.Id, ServiceCatalog.QuickVisitName, DateTime.Now.AddDays(-1)));
    }

    [Fact]
    public async Task Preference_is_remembered_on_the_customer_profile()
    {
        await using var harness = await IncomingSmsProcessorTests.Harness.CreateAsync();
        await harness.Processor.ProcessAsync(new IncomingSmsMessage
        {
            From = "3605550302",
            Body = "Hi my name is Riley"
        }, sendReplyViaProvider: false);

        await harness.Processor.ProcessAsync(new IncomingSmsMessage
        {
            From = "3605550302",
            Body = "I prefer afternoon appointments"
        }, sendReplyViaProvider: false);

        var customer = await harness.Db.Customers
            .Include(x => x.ConversationState)
            .Include(x => x.IntentMemory)
            .SingleAsync();
        Assert.Contains("prefer", customer.Notes ?? "", StringComparison.OrdinalIgnoreCase);
        Assert.Contains("prefer", customer.ConversationState?.Preferences ?? "", StringComparison.OrdinalIgnoreCase);
        Assert.False(string.IsNullOrWhiteSpace(customer.IntentMemory?.Summary));
        Assert.True((customer.ConversationState?.TurnCount ?? 0) >= 2);
    }
}
