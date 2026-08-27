using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using REIGN.API.Messaging;
using REIGN.API.Options;
using Xunit;

namespace REIGN.Tests;

public class SkipCallsSmsSenderTests
{
    [Fact]
    public async Task Send_finds_contact_then_posts_sms()
    {
        var handler = new SequenceHandler(
            """{"contacts":[{"id":"ct-1","phoneNumber":"+13609261856"}]}""",
            """{"id":"sms-9"}""");
        using var http = new HttpClient(handler);
        var sender = CreateSender(http);

        var result = await sender.SendAsync(new SmsSendRequest
        {
            To = "+13609261856",
            Body = "QV is on the schedule."
        });

        Assert.True(result.Succeeded);
        Assert.Equal("sms-9", result.ProviderMessageId);
        Assert.Equal(2, handler.Requests.Count);
        Assert.Contains("/users/me/contacts/search", handler.Requests[0].Url, StringComparison.Ordinal);
        Assert.Contains("/users/me/contacts/ct-1/send-sms", handler.Requests[1].Url, StringComparison.Ordinal);
        Assert.Contains("QV is on the schedule.", handler.Requests[1].Body, StringComparison.Ordinal);
        Assert.Contains("agent-22", handler.Requests[1].Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Send_syncs_contact_when_search_is_empty()
    {
        var handler = new SequenceHandler(
            """{"contacts":[]}""",
            """{"success":true}""",
            """{"contacts":[{"id":"ct-2","phoneNumber":"+13609261856"}]}""",
            """{"id":"sms-10"}""");
        using var http = new HttpClient(handler);
        var sender = CreateSender(http);

        var result = await sender.SendAsync(new SmsSendRequest
        {
            To = "3609261856",
            Body = "Hi"
        });

        Assert.True(result.Succeeded);
        Assert.Equal(4, handler.Requests.Count);
        Assert.Contains("/users/me/contacts/sync", handler.Requests[1].Url, StringComparison.Ordinal);
        Assert.Contains("Miss Reign", handler.Requests[1].Body, StringComparison.Ordinal);
        Assert.Contains("/users/me/contacts/ct-2/send-sms", handler.Requests[3].Url, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Send_refuses_the_skipcalls_number()
    {
        var handler = new SequenceHandler("""{"id":"nope"}""");
        using var http = new HttpClient(handler);
        var sender = CreateSender(http, fromNumber: "+15551239999");

        var result = await sender.SendAsync(new SmsSendRequest
        {
            To = "+15551239999",
            Body = "should not send"
        });

        Assert.False(result.Succeeded);
        Assert.Contains("SkipCalls", result.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(handler.Requests);
    }

    private static SkipCallsSmsSender CreateSender(HttpClient http, string fromNumber = "+15555550100") =>
        new(
            http,
            Options.Create(new SmsOptions
            {
                BusinessPhoneNumber = fromNumber,
                SkipCalls = new SkipCallsOptions
                {
                    AccessToken = "token-abc",
                    FromNumber = fromNumber,
                    AgentId = "agent-22"
                }
            }),
            NullLogger<SkipCallsSmsSender>.Instance);

    private sealed class SequenceHandler : HttpMessageHandler
    {
        private readonly Queue<string> _responses;

        public SequenceHandler(params string[] responses) =>
            _responses = new Queue<string>(responses);

        public List<(string Url, string Body)> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var body = request.Content == null
                ? ""
                : await request.Content.ReadAsStringAsync(cancellationToken);
            Requests.Add((request.RequestUri?.ToString() ?? "", body));
            var json = _responses.Count > 0 ? _responses.Dequeue() : """{"ok":true}""";
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
        }
    }
}
