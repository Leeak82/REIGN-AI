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
        var connected = database == "connected";

        var body = new
        {
            status = connected ? "healthy" : "degraded",
            database,
            groqConfigured,
            smsConfigured,
            calendarConfigured,
            calendarProvider = CalendarProvider()
        };

        // Stay 200 when the password is wrong so Render does not restart the container.
        return Ok(body);
    }

    [HttpGet]
    public IActionResult Get()
    {
        return Ok(new
        {
            status = "ok",
            service = "REIGN.API",
            utc = DateTime.UtcNow,
            databaseStatus = !string.IsNullOrWhiteSpace(_configuration.GetConnectionString("Reign"))
                || !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SUPABASE_DB_PASSWORD"))
                ? "configured"
                : "not configured",
            groqConfigured = _ai.IsConfigured || GroqConfigured(),
            smsConfigured = SmsConfigured(),
            calendarConfigured = CalendarConfigured(),
            calendarProvider = CalendarProvider()
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
            return false;
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

        if (provider.Equals("SmsGate", StringComparison.OrdinalIgnoreCase) ||
            provider.Equals("Android", StringComparison.OrdinalIgnoreCase))
        {
            return !string.IsNullOrWhiteSpace(_sms.SmsGate.Username) &&
                   !string.IsNullOrWhiteSpace(_sms.SmsGate.Password);
        }

        if (provider.Equals("SkipCalls", StringComparison.OrdinalIgnoreCase) ||
            provider.Equals("Skip-Calls", StringComparison.OrdinalIgnoreCase) ||
            provider.Equals("Cail", StringComparison.OrdinalIgnoreCase))
        {
            return !string.IsNullOrWhiteSpace(_sms.SkipCalls.AccessToken);
        }

        return false;
    }

    private string CalendarProvider() =>
        string.IsNullOrWhiteSpace(_google.Provider) ? "Simulated" : _google.Provider.Trim();

    private bool CalendarConfigured()
    {
        var provider = CalendarProvider();
        if (provider.Equals("Simulated", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return !string.IsNullOrWhiteSpace(_google.ClientId) &&
               !string.IsNullOrWhiteSpace(_google.ClientSecret);
    }
}
