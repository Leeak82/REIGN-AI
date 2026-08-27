using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using REIGN.API.Messaging;
using REIGN.API.Options;
using REIGN.Core.Contact;
using Xunit;

namespace REIGN.Tests;

public class SmsGateSmsSenderTests
{
    [Fact]
    public async Task Send_pins_device_id_and_sim_slot()
    {
        var handler = new CaptureHandler("""{"id":"msg-1"}""");
        using var http = new HttpClient(handler);
        var options = Options.Create(new SmsOptions
        {
            BusinessPhoneNumber = ReignContact.BusinessPhoneE164,
            SmsGate = new SmsGateOptions
            {
                Username = "user",
                Password = "pass",
                FromNumber = ReignContact.BusinessPhoneE164,
                DeviceId = "device-abc",
                SimNumber = 1
            }
        });
        var sender = new SmsGateSmsSender(http, options, NullLogger<SmsGateSmsSender>.Instance);

        var result = await sender.SendAsync(new SmsSendRequest
        {
            To = "+15555550123",
            Body = "QV is on the schedule."
        });

        Assert.True(result.Succeeded);
        Assert.Contains("\"deviceId\":\"device-abc\"", handler.Body, StringComparison.Ordinal);
        Assert.Contains("\"simNumber\":1", handler.Body, StringComparison.Ordinal);
        Assert.Contains("\"priority\":100", handler.Body, StringComparison.Ordinal);
        Assert.Contains("phoneNumbers", handler.Body, StringComparison.Ordinal);
        Assert.Contains("5555550123", handler.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Send_refuses_the_gateway_phone()
    {
        var handler = new CaptureHandler("""{"id":"msg-1"}""");
        using var http = new HttpClient(handler);
        var options = Options.Create(new SmsOptions
        {
            BusinessPhoneNumber = ReignContact.BusinessPhoneE164,
            SmsGate = new SmsGateOptions
            {
                Username = "user",
                Password = "pass",
                FromNumber = ReignContact.BusinessPhoneE164,
                DeviceId = "device-abc",
                SimNumber = 1,
                IgnoreFromNumbers = "+19072132242"
            }
        });
        var sender = new SmsGateSmsSender(http, options, NullLogger<SmsGateSmsSender>.Instance);

        var toBusiness = await sender.SendAsync(new SmsSendRequest
        {
            To = ReignContact.BusinessPhoneE164,
            Body = "should not send"
        });
        Assert.False(toBusiness.Succeeded);
        Assert.Contains("gateway", toBusiness.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("", handler.Body);

        var toCompanion = await sender.SendAsync(new SmsSendRequest
        {
            To = "+19072132242",
            Body = "should not send"
        });
        Assert.False(toCompanion.Succeeded);
        Assert.Equal("", handler.Body);
    }

    private sealed class CaptureHandler : HttpMessageHandler
    {
        private readonly string _response;

        public CaptureHandler(string response) => _response = response;

        public string Body { get; private set; } = "";

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Body = request.Content == null
                ? ""
                : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_response, Encoding.UTF8, "application/json")
            };
        }
    }
}
