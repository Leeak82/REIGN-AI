using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using REIGN.API.Options;
using REIGN.Core.AI;

namespace REIGN.API.Controllers;

[ApiController]
[Route("api/health")]
public class HealthController : ControllerBase
{
    private readonly IAiProvider _ai;
    private readonly IConfiguration _configuration;
    private readonly AiOptions _aiOptions;
    private readonly SmsOptions _sms;
    private readonly GoogleCalendarOptions _google;

    public HealthController(
        IAiProvider ai,
        IConfiguration configuration,
        IOptions<AiOptions> aiOptions,
        IOptions<SmsOptions> sms,
        IOptions<GoogleCalendarOptions> google)
    {
        _ai = ai;
        _configuration = configuration;
        _aiOptions = aiOptions.Value;
        _sms = sms.Value;
        _google = google.Value;
    }

    [HttpGet]
    public IActionResult Get()
    {
        return Ok(new
        {
            status = "ok",
            service = "REIGN.API",
            utc = DateTime.UtcNow,
            databaseConfigured = !string.IsNullOrWhiteSpace(_configuration.GetConnectionString("Reign")),
            ai = new
            {
                provider = _ai.ProviderName,
                groqConfigured = _ai.IsConfigured || !string.IsNullOrWhiteSpace(_aiOptions.ApiKey),
                fallbackAvailable = true
            },
            sms = new
            {
                provider = _sms.Provider,
                businessNumberConfigured = !string.IsNullOrWhiteSpace(_sms.BusinessPhoneNumber)
            },
            calendar = new
            {
                provider = _google.Provider,
                oauthClientConfigured =
                    !string.IsNullOrWhiteSpace(_google.ClientId) &&
                    !string.IsNullOrWhiteSpace(_google.ClientSecret)
            }
        });
    }
}
