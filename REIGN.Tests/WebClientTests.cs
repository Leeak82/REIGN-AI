using System.Net;
using System.Text;
using Microsoft.Extensions.Configuration;
using REIGN.Web;
using REIGN.Web.Services;
using Xunit;

namespace REIGN.Tests;

public class WebClientTests
{
    [Fact]
    public void HostAliasOverridesCheckedInLocalDefaults()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ReignApi:BaseUrl"] = "http://localhost:5012/",
            ["ApiBaseUrl"] = "http://localhost:5012/",
            ["REIGN_API_BASE_URL"] = "https://api.example.test/reign"
        }).Build();
        Assert.Equal("https://api.example.test/reign/", ApiEndpoint.Resolve(configuration));
    }

    [Theory]
    [InlineData("file:///tmp/api")]
    [InlineData("https://user:secret@example.test")]
    [InlineData("https://example.test?secret=value")]
    [InlineData("relative/path")]
    public void InvalidEndpointFailsWithoutEchoingConfiguration(string value)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        { ["REIGN_API_BASE_URL"] = value }).Build();
        var error = Assert.Throws<InvalidOperationException>(() => ApiEndpoint.Resolve(configuration));
        Assert.DoesNotContain(value, error.Message);
    }

    [Theory]
    [InlineData(HttpStatusCode.Conflict, "{\"error\":\"That time is not available.\"}", "That time is not available.")]
    [InlineData(HttpStatusCode.NotFound, "", "404")]
    [InlineData(HttpStatusCode.BadGateway, "<html>Unavailable</html>", "502")]
    public async Task AppointmentFailuresAreRecoverable(HttpStatusCode status, string body, string expected)
    {
        var client = Client(status, body);
        var result = await client.ConfirmAppointment(Guid.NewGuid());
        Assert.Contains(expected, result!.Error);
        Assert.Contains(expected, (await client.CancelAppointment(Guid.NewGuid()))!.Error);
        Assert.Contains(expected, (await client.RescheduleAppointment(Guid.NewGuid(), DateTime.Today.AddDays(1)))!.Error);
        Assert.Contains(expected, (await client.CreateAppointment("+15555550123", "Hour", DateTime.Today.AddDays(1)))!.Error);
    }

    [Fact]
    public async Task ConfirmationPreservesPartialCalendarFailure()
    {
        var result = await Client(HttpStatusCode.OK,
            "{\"message\":\"Appointment confirmed\",\"calendarSynced\":false,\"calendarSyncError\":\"Reconnect Google\"}")
            .ConfirmAppointment(Guid.NewGuid());
        Assert.Null(result!.Error);
        Assert.Equal("Reconnect Google", result.CalendarSyncError);
    }

    [Theory]
    [InlineData("{\"sent\":false,\"error\":\"Provider unavailable\"}")]
    [InlineData("{\"sent\":false}")]
    [InlineData("null")]
    public async Task UnconfirmedSmsNeverReportsSuccess(string body)
    {
        await Assert.ThrowsAsync<HttpRequestException>(() => Client(HttpStatusCode.OK, body)
            .SendOwnerSms("+15555550123", "test"));
    }

    [Fact]
    public async Task SimulatedSmsIsClearlyLabeled()
    {
        Assert.Contains("simulated", await Client(HttpStatusCode.OK, "{\"sent\":true,\"simulated\":true}")
            .SendOwnerSms("+15555550123", "test"));
    }

    [Fact]
    public async Task ResumeDoesNotIgnoreMissingCustomer()
    {
        await Assert.ThrowsAsync<HttpRequestException>(() => Client(HttpStatusCode.NotFound, "")
            .ResumeAssistant("+15555550123"));
    }

    private static ReignApiClient Client(HttpStatusCode status, string body) => new(new HttpClient(new Handler(status, body))
    { BaseAddress = new Uri("https://api.example.test/") });

    private sealed class Handler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(status)
            { Content = new StringContent(body, Encoding.UTF8, "application/json") });
    }
}
