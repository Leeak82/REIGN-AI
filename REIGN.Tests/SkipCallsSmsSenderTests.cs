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
        Assert.Contains("query=13609261856", handler.Requests[0].Url, StringComparison.Ordinal);
        Assert.DoesNotContain("%2B", handler.Requests[0].Url, StringComparison.Ordinal);
        Assert.Contains("/users/me/contacts/ct-1/send-sms", handler.Requests[1].Url, StringComparison.Ordinal);
        Assert.Contains("QV is on the schedule.", handler.Requests[1].Body, StringComparison.Ordinal);
        Assert.Contains("agent-22", handler.Requests[1].Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Send_finds_contact_when_plus_search_is_empty()
    {
        var handler = new QueryHandler((url, _) =>
        {
            if (url.Contains("/send-sms", StringComparison.Ordinal))
            {
                return """{"id":"sms-11"}""";
            }

            if (url.Contains("query=%2B", StringComparison.Ordinal) ||
                url.Contains("query=+", StringComparison.Ordinal))
            {
                return """{"contacts":[]}""";
            }

            return """{"contacts":[{"id":"ct-live","phoneNumber":"12538319100"}]}""";
        });
        using var http = new HttpClient(handler);
        var sender = CreateSender(http);

        var result = await sender.SendAsync(new SmsSendRequest
        {
            To = "+12538319100",
            Body = "Hello from Miss Reign."
        });

        Assert.True(result.Succeeded);
        Assert.Equal("sms-11", result.ProviderMessageId);
        Assert.DoesNotContain(handler.Requests, r => r.Url.Contains("/contacts/sync", StringComparison.Ordinal));
        Assert.Contains(handler.Requests, r => r.Url.Contains("query=12538319100", StringComparison.Ordinal));
        Assert.Contains(handler.Requests, r => r.Url.Contains("/contacts/ct-live/send-sms", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Send_syncs_contact_when_search_is_empty()
    {
        var handler = new SequenceHandler(
            """{"contacts":[]}""",
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
        Assert.Equal(5, handler.Requests.Count);
        Assert.Contains("query=13609261856", handler.Requests[0].Url, StringComparison.Ordinal);
        Assert.Contains("query=3609261856", handler.Requests[1].Url, StringComparison.Ordinal);
        Assert.Contains("/users/me/contacts/sync", handler.Requests[2].Url, StringComparison.Ordinal);
        Assert.Contains("Miss Reign", handler.Requests[2].Body, StringComparison.Ordinal);
        Assert.Contains("\"phoneNumber\":\"13609261856\"", handler.Requests[2].Body, StringComparison.Ordinal);
        Assert.DoesNotContain("\"phoneNumber\":\"+13609261856\"", handler.Requests[2].Body, StringComparison.Ordinal);
        Assert.Contains("/users/me/contacts/ct-2/send-sms", handler.Requests[4].Url, StringComparison.Ordinal);
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

    [Fact]
    public void SearchQueries_uses_digits_not_plus_e164()
    {
        Assert.Equal(["12538319100", "2538319100"], SkipCallsSmsSender.SearchQueries("+12538319100"));
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

    private sealed class QueryHandler : HttpMessageHandler
    {
        private readonly Func<string, string, string> _respond;

        public QueryHandler(Func<string, string, string> respond) => _respond = respond;

        public List<(string Url, string Body)> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var body = request.Content == null
                ? ""
                : await request.Content.ReadAsStringAsync(cancellationToken);
            var url = request.RequestUri?.ToString() ?? "";
            Requests.Add((url, body));
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_respond(url, body), Encoding.UTF8, "application/json")
            };
        }
    }
}
