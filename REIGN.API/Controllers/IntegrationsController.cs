using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using REIGN.API.Calendar;
using REIGN.API.Messaging;
using REIGN.API.Options;

namespace REIGN.API.Controllers;

[ApiController]
[Route("api/integrations")]
public class IntegrationsController : ControllerBase
{
    private readonly ConfigurableSmsSender _sms;
    private readonly ConfigurableCalendarService _calendar;
    private readonly GoogleCalendarOptions _google;
    private readonly SmsOptions _smsOptions;

    public IntegrationsController(
        ConfigurableSmsSender sms,
        ConfigurableCalendarService calendar,
        IOptions<GoogleCalendarOptions> google,
        IOptions<SmsOptions> smsOptions)
    {
        _sms = sms;
        _calendar = calendar;
        _google = google.Value;
        _smsOptions = smsOptions.Value;
    }

    [HttpGet("status")]
    public IActionResult Status()
    {
        return Ok(new
        {
            sms = new
            {
                configuredProvider = _smsOptions.Provider,
                activeProvider = _sms.ProviderName,
                simulated = _sms.IsSimulated,
                credentialsPresent = _sms.IsConfigured,
                businessPhoneConfigured = !string.IsNullOrWhiteSpace(_smsOptions.BusinessPhoneNumber),
                ownerPhoneConfigured = !string.IsNullOrWhiteSpace(_smsOptions.OwnerPhoneNumber),
                textNowSupported = false,
                textNowReason = TextNowUnsupportedSmsSender.Reason
            },
            googleCalendar = new
            {
                configuredProvider = _google.Provider,
                activeProvider = _calendar.ProviderName,
                simulated = _calendar.IsSimulated,
                oauthClientConfigured = _calendar.IsConfigured ||
                    (!string.IsNullOrWhiteSpace(_google.ClientId) && !string.IsNullOrWhiteSpace(_google.ClientSecret)),
                hasStoredGrant = _calendar.HasStoredGrant,
                calendarId = _google.CalendarId
            }
        });
    }

    [HttpGet("google/authorize")]
    public IActionResult GoogleAuthorize()
    {
        if (string.IsNullOrWhiteSpace(_google.ClientId) || string.IsNullOrWhiteSpace(_google.ClientSecret))
        {
            return StatusCode(503, new
            {
                error = "Google Calendar OAuth client is not configured. Set GoogleCalendar:ClientId and GoogleCalendar:ClientSecret."
            });
        }

        var url =
            "https://accounts.google.com/o/oauth2/v2/auth" +
            $"?client_id={Uri.EscapeDataString(_google.ClientId)}" +
            $"&redirect_uri={Uri.EscapeDataString(_google.RedirectUri)}" +
            "&response_type=code" +
            $"&scope={Uri.EscapeDataString("https://www.googleapis.com/auth/calendar.events")}" +
            "&access_type=offline" +
            "&prompt=consent";

        return Redirect(url);
    }

    [HttpGet("google/callback")]
    public async Task<IActionResult> GoogleCallback([FromQuery] string? code, [FromQuery] string? error)
    {
        if (!string.IsNullOrWhiteSpace(error))
        {
            return BadRequest(new { error });
        }

        if (string.IsNullOrWhiteSpace(code))
        {
            return BadRequest(new { error = "Missing authorization code." });
        }

        try
        {
            await _calendar.StoreAuthorizationCodeAsync(code);
            return Ok(new { connected = true, provider = "GoogleCalendar" });
        }
        catch (Exception ex)
        {
            return StatusCode(502, new { error = "Google OAuth exchange failed.", detail = ex.Message });
        }
    }
}
