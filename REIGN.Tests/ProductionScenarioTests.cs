using Microsoft.EntityFrameworkCore;
using REIGN.API.Messaging;
using REIGN.Core.Catalog;
using Xunit;

namespace REIGN.Tests;

public class ProductionScenarioTests
{
    [Fact]
    public async Task Scenario1_business_question_does_not_create_an_appointment()
    {
        await using var harness = await IncomingSmsProcessorTests.Harness.CreateAsync();

        var result = await harness.Processor.ProcessAsync(new IncomingSmsMessage
        {
            From = "3605550201",
            Body = "How much is a QV?"
        }, sendReplyViaProvider: false);

        Assert.Equal("business_question", result.Intent);
        Assert.Contains("150", result.Reply);
        Assert.DoesNotContain("YES to confirm", result.Reply, StringComparison.OrdinalIgnoreCase);
        Assert.True(result.Persisted);
        Assert.Empty(harness.Db.Appointments);
        Assert.Equal(2, await harness.Db.ConversationMessages.CountAsync());
    }

    [Fact]
    public async Task Scenario2_customer_schedules_an_appointment()
    {
        await using var harness = await IncomingSmsProcessorTests.Harness.CreateAsync();

        var asked = await harness.Processor.ProcessAsync(new IncomingSmsMessage
        {
            From = "3605550202",
            Body = "I want to book a half hour"
        }, sendReplyViaProvider: false);

        Assert.Equal("schedule", asked.Intent);
        Assert.Contains("Half Hour", asked.Reply, StringComparison.OrdinalIgnoreCase);

        var booked = await harness.Processor.ProcessAsync(new IncomingSmsMessage
        {
            From = "3605550202",
            Body = "tomorrow at 2 pm"
        }, sendReplyViaProvider: false);

        Assert.Contains("YES", booked.Reply, StringComparison.OrdinalIgnoreCase);
        var appointment = Assert.Single(harness.Db.Appointments.Include(x => x.Service));
        Assert.Equal(ServiceCatalog.HalfHourName, appointment.Service.Name);
        Assert.Equal(300m, appointment.Price);
        Assert.Equal("Pending", appointment.Status);

        var confirmed = await harness.Processor.ProcessAsync(new IncomingSmsMessage
        {
            From = "3605550202",
            Body = "YES"
        }, sendReplyViaProvider: false);

        Assert.Equal("confirm", confirmed.Intent);
        Assert.Contains("Confirmed", confirmed.Reply, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("Confirmed", (await harness.Db.Appointments.SingleAsync()).Status);
        Assert.False(string.IsNullOrWhiteSpace((await harness.Db.Appointments.SingleAsync()).ExternalCalendarEventId));
        Assert.Single(harness.Calendar.Events);
    }

    [Fact]
    public async Task Scenario3_returning_customer_remembers_context()
    {
        await using var harness = await IncomingSmsProcessorTests.Harness.CreateAsync();

        await harness.Processor.ProcessAsync(new IncomingSmsMessage
        {
            From = "3605550203",
            Body = "Hi my name is Jordan"
        }, sendReplyViaProvider: false);

        await harness.Processor.ProcessAsync(new IncomingSmsMessage
        {
            From = "3605550203",
            Body = "Book QV tomorrow at 11 am"
        }, sendReplyViaProvider: false);

        var returning = await harness.Processor.ProcessAsync(new IncomingSmsMessage
        {
            From = "3605550203",
            Body = "What services do you offer?"
        }, sendReplyViaProvider: false);

        var customer = await harness.Db.Customers.SingleAsync();
        Assert.Equal("Jordan", customer.Name);
        Assert.True(customer.TurnCount >= 3);
        Assert.False(string.IsNullOrWhiteSpace(customer.MemorySummary));
        Assert.False(string.IsNullOrWhiteSpace(customer.IntentHistory));
        Assert.Contains("Quick Visit", customer.PendingServiceName ?? "", StringComparison.OrdinalIgnoreCase);
        Assert.True(returning.Persisted);
        Assert.True(
            returning.Reply!.Contains("Returning", StringComparison.OrdinalIgnoreCase) ||
            returning.Reply.Contains("Jordan", StringComparison.OrdinalIgnoreCase) ||
            returning.Reply.Contains("150"));
    }

    [Fact]
    public async Task Scenario4_owner_asks_for_customer_activity()
    {
        await using var harness = await IncomingSmsProcessorTests.Harness.CreateAsync();

        await harness.Processor.ProcessAsync(new IncomingSmsMessage
        {
            From = "3605550204",
            Body = "Book an hour tomorrow at 4 pm"
        }, sendReplyViaProvider: false);

        var owner = await harness.OwnerAssistant.AnswerAsync("How many customers and what's on the book?");

        Assert.Contains("customers", owner, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Hour", owner, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("$150", owner);
        Assert.Contains("$300", owner);
        Assert.Contains("$500", owner);

        var fromOwnerPhone = await harness.Processor.ProcessAsync(new IncomingSmsMessage
        {
            From = "+15555550199",
            Body = "Give me today's activity"
        }, sendReplyViaProvider: false);

        Assert.True(fromOwnerPhone.OwnerQueryHandled);
        Assert.Contains("customers", fromOwnerPhone.Reply, StringComparison.OrdinalIgnoreCase);
    }
}
