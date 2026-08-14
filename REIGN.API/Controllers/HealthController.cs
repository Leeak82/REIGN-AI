using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using REIGN.API.Options;
using REIGN.Core.AI;
using REIGN.Data;

namespace REIGN.API.Controllers;

[ApiController]
[Route("api/health")]
public class HealthController : ControllerBase
{
    private readonly IAiProvider _ai;
    private readonly IConfiguration _configuration;
    private readonly ReignDbContext _db;
    private readonly AiOptions _aiOptions;
    private readonly SmsOptions _sms;
    private readonly GoogleCalendarOptions _google;

    public HealthController(
        IAiProvider ai,
        IConfiguration configuration,
        ReignDbContext db,
        IOptions<AiOptions> aiOptions,
        IOptions<SmsOptions> sms,
        IOptions<GoogleCalendarOptions> google)
    {
        _ai = ai;
        _configuration = configuration;
        _db = db;
        _aiOptions = aiOptions.Value;
        _sms = sms.Value;
        _google = google.Value;
    }

    [HttpGet("/health")]
    public async Task<IActionResult> Production()
    {
        var database = await ProbeDatabaseAsync();
        var groqConfigured = GroqConfigured();
        var smsConfigured = SmsConfigured();
        var calendarConfigured = CalendarConfigured();
        var healthy = database == "connected";

        var body = new
        {
            status = healthy ? "healthy" : "unhealthy",
            database,
            groqConfigured,
            smsConfigured,
            calendarConfigured
        };

        return healthy ? Ok(body) : StatusCode(503, body);
    }

    [HttpGet]
    public IActionResult Get()
    {
        return Ok(new
        {
            status = "ok",
            service = "REIGN.API",
            Status = "REIGN API Online",
            utc = DateTime.UtcNow,
            Time = DateTime.UtcNow,
            databaseConfigured = !string.IsNullOrWhiteSpace(_configuration.GetConnectionString("Reign")),
            ai = new
            {
                provider = _ai.ProviderName,
                groqConfigured = _ai.IsConfigured || GroqConfigured(),
                fallbackAvailable = true
            },
            sms = new
            {
                provider = _sms.Provider,
                configured = SmsConfigured(),
                businessNumberConfigured = !string.IsNullOrWhiteSpace(_sms.BusinessPhoneNumber)
            },
            calendar = new
            {
                provider = _google.Provider,
                configured = CalendarConfigured(),
                oauthClientConfigured =
                    !string.IsNullOrWhiteSpace(_google.ClientId) &&
                    !string.IsNullOrWhiteSpace(_google.ClientSecret)
            }
        });
    }

    private async Task<string> ProbeDatabaseAsync()
    {
        try
        {
            return await _db.Database.CanConnectAsync() ? "connected" : "disconnected";
        }
        catch
        {
            return "disconnected";
        }
    }

    private bool GroqConfigured() =>
        !string.IsNullOrWhiteSpace(_aiOptions.ApiKey);

    private bool SmsConfigured()
    {
        var provider = _sms.Provider ?? "Simulated";
        if (provider.Equals("Simulated", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (provider.Equals("Twilio", StringComparison.OrdinalIgnoreCase))
        {
            return !string.IsNullOrWhiteSpace(_sms.Twilio.AccountSid) &&
                   !string.IsNullOrWhiteSpace(_sms.Twilio.AuthToken);
        }

        if (provider.Equals("Vonage", StringComparison.OrdinalIgnoreCase))
        {
            return !string.IsNullOrWhiteSpace(_sms.Vonage.ApiKey) &&
                   !string.IsNullOrWhiteSpace(_sms.Vonage.ApiSecret);
        }

        return false;
    }

    private bool CalendarConfigured()
    {
        var provider = _google.Provider ?? "Simulated";
        if (provider.Equals("Simulated", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return !string.IsNullOrWhiteSpace(_google.ClientId) &&
               !string.IsNullOrWhiteSpace(_google.ClientSecret);
    }
}
