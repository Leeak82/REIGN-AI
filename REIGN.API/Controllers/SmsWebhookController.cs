using System.Text;
using Microsoft.AspNetCore.Mvc;
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
    private readonly SmsOptions _options;
    private readonly ILogger<SmsWebhookController> _logger;

    public SmsWebhookController(
        IncomingSmsProcessor processor,
        IOptions<SmsOptions> options,
        ILogger<SmsWebhookController> logger)
    {
        _processor = processor;
        _options = options.Value;
        _logger = logger;
    }

    [HttpPost("twilio")]
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
                "Rejected Twilio webhook with invalid signature. Tried {Count} public URL candidates: {Urls}. Sending SMS from the Twilio Console does not hit this endpoint; the phone number A Message Comes In webhook must POST to https://YOUR_HOST/api/sms/webhooks/twilio using the same Auth Token as TWILIO_AUTH_TOKEN.",
                candidates.Count,
                string.Join(" | ", candidates));
            return Forbid();
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

    private async Task ProcessSafelyAsync(string provider, IncomingSmsMessage incoming)
    {
        try
        {
            var result = await _processor.ProcessAsync(incoming, sendReplyViaProvider: true);
            if (result.Outbound is { Succeeded: false })
            {
                _logger.LogWarning(
                    "{Provider} inbound processed for {Phone} but outbound send failed: {Error}. The Twilio From number must be the dedicated Twilio number (TWILIO_FROM_NUMBER), not the owner cell. Console Send SMS does not use this From check.",
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
}
