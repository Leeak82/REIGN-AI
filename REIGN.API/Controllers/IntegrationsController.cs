using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using REIGN.API.Calendar;
using REIGN.API.Configuration;
using REIGN.API.Messaging;
using REIGN.API.Options;

namespace REIGN.API.Controllers;

[ApiController]
[Route("api/integrations")]
public class IntegrationsController : ControllerBase
{
    private readonly ConfigurableSmsSender _sms;
    private readonly ConfigurableCalendarService _calendar;
    private readonly GoogleCalendarService _googleCalendar;
    private readonly GoogleCalendarOptions _google;
    private readonly SmsOptions _smsOptions;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<IntegrationsController> _logger;

    public IntegrationsController(
        ConfigurableSmsSender sms,
        ConfigurableCalendarService calendar,
        GoogleCalendarService googleCalendar,
        IOptions<GoogleCalendarOptions> google,
        IOptions<SmsOptions> smsOptions,
        IHostEnvironment environment,
        ILogger<IntegrationsController> logger)
    {
        _sms = sms;
        _calendar = calendar;
        _googleCalendar = googleCalendar;
        _google = google.Value;
        _smsOptions = smsOptions.Value;
        _environment = environment;
        _logger = logger;
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
                oauthClientConfigured =
                    !string.IsNullOrWhiteSpace(_google.ClientId) &&
                    !string.IsNullOrWhiteSpace(_google.ClientSecret),
                oauthClientId = string.IsNullOrWhiteSpace(_google.ClientId) ? null : _google.ClientId,
                oauthClientSecretLooksLikeWeb = GoogleOAuthCredentials.LooksLikeWebClientSecret(_google.ClientSecret),
                hasStoredGrant = !_calendar.IsSimulated && _calendar.HasStoredGrant,
                calendarId = string.IsNullOrWhiteSpace(_google.CalendarId) ? "primary" : _google.CalendarId,
                timeZone = CalendarTime.ToGoogleTimeZoneId(_google.TimeZone),
                redirectUri = EffectiveRedirectUri(),
                platformRedirectUri = GoogleRedirectUri.TryPlatformPublicCallback(),
                requiredScope = GoogleCalendarService.RequiredScope,
                expectedAccount = ExpectedGoogleAccount()
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
                error = "Google Calendar OAuth client is not configured. Set GoogleCalendar__ClientId and GoogleCalendar__ClientSecret."
            });
        }

        var url = GoogleCalendarService.BuildAuthorizationUrl(_google.ClientId, EffectiveRedirectUri());

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

        var redirectUri = EffectiveRedirectUri();
        try
        {
            await _calendar.StoreAuthorizationCodeAsync(code, redirectUri);
            return Ok(new { connected = true, provider = "GoogleCalendar", redirectUri });
        }
        catch (GoogleOAuthException ex)
        {
            _logger.LogWarning(
                ex,
                "Google OAuth code exchange failed ({GoogleError}) for {RedirectUri}.",
                ex.GoogleError,
                ex.RedirectUri);
            return StatusCode(502, new
            {
                error = string.IsNullOrWhiteSpace(ex.GoogleError)
                    ? "Google OAuth exchange failed. Confirm ClientId, ClientSecret, and RedirectUri, then authorize again."
                    : $"Google OAuth exchange failed ({ex.GoogleError}). Confirm ClientId, ClientSecret, and RedirectUri, then authorize again.",
                googleError = ex.GoogleError,
                googleErrorDescription = string.IsNullOrWhiteSpace(ex.GoogleErrorDescription)
                    ? null
                    : GoogleCalendarService.SanitizeDiagnosticText(ex.GoogleErrorDescription),
                redirectUri = ex.RedirectUri,
                httpStatus = ex.HttpStatus
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Google OAuth code exchange failed.");
            return StatusCode(502, new
            {
                error = "Google OAuth exchange failed. Confirm ClientId, ClientSecret, and RedirectUri, then authorize again.",
                detail = GoogleCalendarService.SanitizeDiagnosticText(ex.Message),
                redirectUri
            });
        }
    }

    /// <summary>
    /// Development-only diagnostic: GET a Google Calendar event with the stored OAuth grant.
    /// </summary>
    [HttpGet("google/debug-event/{eventId}")]
    [ApiExplorerSettings(IgnoreApi = true)]
    public async Task<IActionResult> GoogleDebugEvent(string eventId, CancellationToken cancellationToken)
    {
        if (!_environment.IsDevelopment())
        {
            return NotFound();
        }

        var result = await _googleCalendar.GetEventDebugAsync(eventId, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Development-only diagnostic: identify the Google account/calendar the stored OAuth grant maps to.
    /// </summary>
    [HttpGet("google/debug-account")]
    [ApiExplorerSettings(IgnoreApi = true)]
    public async Task<IActionResult> GoogleDebugAccount(CancellationToken cancellationToken)
    {
        if (!_environment.IsDevelopment())
        {
            return NotFound();
        }

        var result = await _googleCalendar.GetAccountDebugAsync(cancellationToken);
        return Ok(result);
    }

    private string EffectiveRedirectUri() =>
        GoogleRedirectUri.EnsureOAuthCallback(
            _google.RedirectUri,
            HttpContext?.Request,
            _environment.IsDevelopment());

    private string? ExpectedGoogleAccount()
    {
        var calendarId = string.IsNullOrWhiteSpace(_google.CalendarId) ? "primary" : _google.CalendarId.Trim();
        return calendarId.Contains('@', StringComparison.Ordinal) ? calendarId : null;
    }
}
