using REIGN.API.Messaging;
using REIGN.API.Services;
using REIGN.Core.AI;
using Xunit;

namespace REIGN.Tests;

public class ConversationNameTests
{
    [Theory]
    [InlineData("Hi my name is Jordan", "Jordan")]
    [InlineData("my name is Alex Rivera", "Alex Rivera")]
    [InlineData("It's Alex", "Alex")]
    [InlineData("its Riley", "Riley")]
    [InlineData("Name is Sam", "Sam")]
    [InlineData("this is Avery", "Avery")]
    [InlineData("I am Jordan", "Jordan")]
    [InlineData("I'm Alex", "Alex")]
    [InlineData("call me Riley", "Riley")]
    [InlineData("Alex", "Alex")]
    [InlineData("Alex Rivera", "Alex Rivera")]
    [InlineData("just Sam", "Sam")]
    [InlineData("Jordan.", "Jordan")]
    public void TryExtractName_recognizes_given_names(string message, string expected)
    {
        Assert.Equal(expected, ConversationService.TryExtractName(message));
    }

    [Theory]
    [InlineData("Hi")]
    [InlineData("hello")]
    [InlineData("YES")]
    [InlineData("QV")]
    [InlineData("Book QV")]
    [InlineData("I am ready")]
    [InlineData("this is next")]
    [InlineData("tomorrow")]
    [InlineData("3 pm")]
    public void TryExtractName_ignores_non_names(string message)
    {
        Assert.Null(ConversationService.TryExtractName(message));
    }

    [Fact]
    public void Standalone_name_is_name_capture_when_missing()
    {
        var intents = new IntentDetectionService();
        var detected = intents.Detect("Alex");
        Assert.Equal(ReignIntentKind.NameCapture, detected.Kind);
    }

    [Fact]
    public async Task Incoming_sms_saves_the_name_after_being_asked()
    {
        await using var harness = await IncomingSmsProcessorTests.Harness.CreateAsync();

        var asked = await harness.Processor.ProcessAsync(new IncomingSmsMessage
        {
            From = "3605550601",
            Body = "Hi Miss Reign"
        }, sendReplyViaProvider: false);

        Assert.Equal("greeting", asked.Intent);
        Assert.Contains("name", asked.Reply, StringComparison.OrdinalIgnoreCase);
        Assert.True(string.IsNullOrWhiteSpace(harness.Db.Customers.Single().Name));

        var named = await harness.Processor.ProcessAsync(new IncomingSmsMessage
        {
            From = "3605550601",
            Body = "Alex"
        }, sendReplyViaProvider: false);

        Assert.Equal("name_capture", named.Intent);
        Assert.Contains("Alex", named.Reply, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("name first", named.Reply, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("Alex", harness.Db.Customers.Single().Name);

        var again = await harness.Processor.ProcessAsync(new IncomingSmsMessage
        {
            From = "3605550601",
            Body = "Hi"
        }, sendReplyViaProvider: false);

        Assert.Contains("Alex", again.Reply, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("name first", again.Reply, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("Alex", harness.Db.Customers.Single().Name);
    }

    [Fact]
    public async Task Incoming_sms_saves_its_full_name()
    {
        await using var harness = await IncomingSmsProcessorTests.Harness.CreateAsync();

        await harness.Processor.ProcessAsync(new IncomingSmsMessage
        {
            From = "3605550602",
            Body = "Hello"
        }, sendReplyViaProvider: false);

        var named = await harness.Processor.ProcessAsync(new IncomingSmsMessage
        {
            From = "3605550602",
            Body = "It's Alex Rivera"
        }, sendReplyViaProvider: false);

        Assert.Equal("name_capture", named.Intent);
        Assert.Contains("Alex Rivera", named.Reply, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("Alex Rivera", harness.Db.Customers.Single().Name);
    }
}
