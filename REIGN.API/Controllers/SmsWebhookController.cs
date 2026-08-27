using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using REIGN.API.Messaging;
using REIGN.API.Options;
using REIGN.API.Services;

namespace REIGN.API.Controllers;

[ApiController]
[Route("api/sms/webhooks")]
public class SmsWebhookController : ControllerBase
{
    private readonly IncomingSmsProcessor _processor;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly SmsOptions _options;
    private readonly ILogger<SmsWebhookController> _logger;

    public SmsWebhookController(
        IncomingSmsProcessor processor,
        IServiceScopeFactory scopeFactory,
        IOptions<SmsOptions> options,
        ILogger<SmsWebhookController> logger)
    {
        _processor = processor;
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// Twilio inbound webhook. Configure the phone number A Message Comes In
    /// callback to HTTP POST /api/sms/incoming (form fields From, To, Body, MessageSid).
    /// /api/sms/webhooks/twilio remains as a compatible alias. JSON POST /api/sms/incoming
    /// is still the Development simulator.
    /// </summary>
    [HttpPost("twilio")]
    [HttpPost("/api/sms/incoming")]
    [Consumes("application/x-www-form-urlencoded")]
    public async Task<IActionResult> Twilio()
    {
        if (string.IsNullOrWhiteSpace(_options.Twilio.AuthToken))
        {
            return StatusCode(503, new { error = "Twilio AuthToken is not configured. Set Sms__Twilio__AuthToken." });
        }

        var form = Request.HasFormContentType
            ? Request.Form.ToDictionary(x => x.Key, x => x.Value.ToString())
            : new Dictionary<string, string>();

        var signature = Request.Headers["X-Twilio-Signature"].ToString();
        var candidates = TwilioWebhookUrlResolver.Candidates(
            Request.Scheme,
            Request.Host.Value,
            Request.Path.Value,
            Request.QueryString.Value,
            _options.Twilio.WebhookPublicUrl,
            _options.PublicBaseUrl,
            Request.Headers["X-Forwarded-Proto"].ToString(),
            Request.Headers["X-Forwarded-Host"].ToString(),
            Environment.GetEnvironmentVariable("RENDER_EXTERNAL_URL"));

        if (!TwilioRequestValidator.IsValidAny(_options.Twilio.AuthToken, candidates, form, signature, out _))
        {
            _logger.LogWarning(
                "Rejected Twilio webhook with invalid signature. Tried {Count} public URL candidates: {Urls}. Sending SMS from the Twilio Console does not hit this endpoint; the phone number A Message Comes In webhook must HTTP POST to https://YOUR_HOST/api/sms/incoming (or /api/sms/webhooks/twilio) using the same Auth Token as TWILIO_AUTH_TOKEN.",
                candidates.Count,
                string.Join(" | ", candidates));
            // StatusCode(403), not Forbid(): there is no authentication scheme, and Forbid() becomes a 500.
            return StatusCode(StatusCodes.Status403Forbidden);
        }

        var incoming = new IncomingSmsMessage
        {
            From = form.GetValueOrDefault("From") ?? "",
            To = form.GetValueOrDefault("To") ?? "",
            Body = form.GetValueOrDefault("Body") ?? "",
            ProviderMessageId = form.GetValueOrDefault("MessageSid"),
            Provider = "Twilio"
        };

        await ProcessSafelyAsync("Twilio", incoming);
        return Content("<Response></Response>", "text/xml");
    }

    [HttpPost("smsgate")]
    public async Task<IActionResult> SmsGate()
    {
        Request.EnableBuffering();
        using var reader = new StreamReader(Request.Body, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true);
        var rawBody = await reader.ReadToEndAsync();
        Request.Body.Position = 0;

        if (_options.SmsGate.RequireSignedWebhooks)
        {
            if (string.IsNullOrWhiteSpace(_options.SmsGate.SigningKey))
            {
                return StatusCode(503, new { error = "SmsGate SigningKey is not configured. Set Sms__SmsGate__SigningKey." });
            }

            var signature = Request.Headers["X-Signature"].ToString();
            var timestamp = Request.Headers["X-Timestamp"].ToString();
            if (!SmsGateWebhookValidator.IsValid(_options.SmsGate.SigningKey, rawBody, timestamp, signature))
            {
                _logger.LogWarning("Rejected SmsGate webhook with invalid HMAC signature.");
                return StatusCode(StatusCodes.Status403Forbidden);
            }
        }

        var incoming = SmsGateWebhookValidator.ParseReceived(rawBody, out var eventName);
        if (incoming.Count == 0)
        {
            _logger.LogInformation("Ignored SmsGate webhook event={Event}", eventName ?? "unknown");
            return Ok(new { ok = true, ignored = true });
        }

        // SmsGate retries unless it gets 2xx within 30s. Groq + Postgres + send
        // can exceed that on a Render free cold start, so ACK first.
        foreach (var message in incoming)
        {
            QueueSmsGate(message);
        }

        return Ok(new { ok = true, queued = incoming.Count });
    }

    [HttpPost("skipcalls")]
    public async Task<IActionResult> SkipCalls()
    {
        Request.EnableBuffering();
        using var reader = new StreamReader(Request.Body, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true);
        var rawBody = await reader.ReadToEndAsync();
        Request.Body.Position = 0;

        if (_options.SkipCalls.RequireSignedWebhooks)
        {
            var expected = FirstNonEmpty(_options.SkipCalls.WebhookSecret, _options.SkipCalls.AccessToken);
            if (string.IsNullOrWhiteSpace(expected))
            {
                return StatusCode(503, new { error = "SkipCalls WebhookSecret is not configured. Set Sms__SkipCalls__WebhookSecret." });
            }

            var provided = new[]
            {
                Request.Query["secret"].ToString(),
                Request.Query["key"].ToString(),
                Request.Headers["X-Webhook-Secret"].ToString(),
                Request.Headers["X-SkipCalls-Secret"].ToString(),
                Request.Headers["X-Api-Key"].ToString(),
                Request.Headers.Authorization.ToString()
            };
            if (!SkipCallsWebhookValidator.IsAuthorized(expected, provided))
            {
                _logger.LogWarning("Rejected SkipCalls webhook with invalid secret.");
                return StatusCode(StatusCodes.Status403Forbidden);
            }
        }

        var incoming = SkipCallsWebhookValidator.TryParseReceived(rawBody);
        if (incoming == null || string.IsNullOrWhiteSpace(incoming.From) || string.IsNullOrWhiteSpace(incoming.Body))
        {
            _logger.LogInformation("Ignored SkipCalls webhook that was not an inbound SMS.");
            return Ok(new { ok = true, ignored = true });
        }

        if (string.IsNullOrWhiteSpace(incoming.To))
        {
            incoming.To = FirstNonEmpty(_options.SkipCalls.FromNumber, _options.BusinessPhoneNumber);
        }

        _logger.LogInformation(
            "SkipCalls inbound queued From={From} To={To} Id={Id}",
            incoming.From,
            incoming.To,
            incoming.ProviderMessageId);
        QueueSmsGate(incoming);
        return Ok(new { ok = true, queued = 1 });
    }

    [HttpPost("vonage")]
    public async Task<IActionResult> Vonage()
    {
        Request.EnableBuffering();
        using var reader = new StreamReader(Request.Body, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true);
        var rawBody = await reader.ReadToEndAsync();
        Request.Body.Position = 0;

        if (_options.Vonage.RequireSignedWebhooks)
        {
            if (string.IsNullOrWhiteSpace(_options.Vonage.SignatureSecret))
            {
                return StatusCode(503, new { error = "Vonage SignatureSecret is not configured. Set Sms__Vonage__SignatureSecret." });
            }

            var authorization = Request.Headers.Authorization.ToString();
            if (!VonageWebhookValidator.TryValidateJwt(_options.Vonage.SignatureSecret, authorization, rawBody))
            {
                _logger.LogWarning("Rejected Vonage webhook with invalid JWT signature.");
                return Unauthorized();
            }
        }

        var incoming = ParseVonage(rawBody, Request.HasFormContentType ? Request.Form.ToDictionary(x => x.Key, x => x.Value.ToString()) : null);
        if (incoming == null || string.IsNullOrWhiteSpace(incoming.From) || string.IsNullOrWhiteSpace(incoming.Body))
        {
            return BadRequest(new { error = "Unable to parse Vonage inbound SMS." });
        }

        await ProcessSafelyAsync("Vonage", incoming);
        return Ok(new { ok = true });
    }

    private void QueueSmsGate(IncomingSmsMessage incoming)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var processor = scope.ServiceProvider.GetRequiredService<IncomingSmsProcessor>();
                await ProcessSafelyAsync(
                    processor,
                    string.IsNullOrWhiteSpace(incoming.Provider) ? "SmsGate" : incoming.Provider,
                    incoming);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SmsGate background processing failed.");
            }
        });
    }

    private async Task ProcessSafelyAsync(string provider, IncomingSmsMessage incoming) =>
        await ProcessSafelyAsync(_processor, provider, incoming);

    private async Task ProcessSafelyAsync(
        IncomingSmsProcessor processor,
        string provider,
        IncomingSmsMessage incoming)
    {
        try
        {
            var result = await processor.ProcessAsync(incoming, sendReplyViaProvider: true);
            if (result.Outbound is { Succeeded: false })
            {
                _logger.LogWarning(
                    "{Provider} inbound processed for {Phone} but outbound send failed: {Error}",
                    provider,
                    result.Phone,
                    result.Outbound.Error);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Provider} webhook processing failed after validation.", provider);
        }
    }

    private IncomingSmsMessage? ParseVonage(string rawBody, Dictionary<string, string>? form)
    {
        if (form != null && form.Count > 0)
        {
            return new IncomingSmsMessage
            {
                From = form.GetValueOrDefault("msisdn") ?? form.GetValueOrDefault("from") ?? "",
                To = form.GetValueOrDefault("to") ?? "",
                Body = form.GetValueOrDefault("text") ?? form.GetValueOrDefault("message") ?? "",
                ProviderMessageId = form.GetValueOrDefault("messageId") ?? form.GetValueOrDefault("message_uuid"),
                Provider = "Vonage"
            };
        }

        if (string.IsNullOrWhiteSpace(rawBody))
        {
            return null;
        }

        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(rawBody);
            var root = doc.RootElement;
            var from = ReadNestedString(root, "from", "number")
                ?? ReadString(root, "from")
                ?? ReadString(root, "msisdn");
            var to = ReadNestedString(root, "to", "number")
                ?? ReadString(root, "to");
            var text = ReadNestedString(root, "message", "content", "text")
                ?? ReadString(root, "text")
                ?? ReadString(root, "message");

            return new IncomingSmsMessage
            {
                From = from ?? "",
                To = to ?? "",
                Body = text ?? "",
                ProviderMessageId = ReadString(root, "message_uuid") ?? ReadString(root, "messageId"),
                Provider = "Vonage"
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Vonage inbound body could not be parsed.");
            return null;
        }
    }

    private static string? ReadString(System.Text.Json.JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == System.Text.Json.JsonValueKind.String
            ? value.GetString()
            : null;

    private static string? ReadNestedString(System.Text.Json.JsonElement element, params string[] path)
    {
        var current = element;
        foreach (var part in path)
        {
            if (!current.TryGetProperty(part, out current))
            {
                return null;
            }
        }

        return current.ValueKind == System.Text.Json.JsonValueKind.String ? current.GetString() : null;
    }

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(static v => !string.IsNullOrWhiteSpace(v)) ?? "";
}
