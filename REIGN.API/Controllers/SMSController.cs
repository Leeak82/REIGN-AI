using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using REIGN.API.Messaging;
using REIGN.API.Options;
using REIGN.API.Services;

namespace REIGN.API.Controllers;

[ApiController]
[Route("api/sms")]
public class SMSController : ControllerBase
{
    private readonly IncomingSmsProcessor _processor;
    private readonly SimulatedSmsSender _simulated;
    private readonly SmsOptions _options;
    private readonly IHostEnvironment _environment;

    public SMSController(
        IncomingSmsProcessor processor,
        SimulatedSmsSender simulated,
        IOptions<SmsOptions> options,
        IHostEnvironment environment)
    {
        _processor = processor;
        _simulated = simulated;
        _options = options.Value;
        _environment = environment;
    }

    /// <summary>
    /// Development JSON simulator. Live Twilio uses the same path with
    /// application/x-www-form-urlencoded (From, To, Body, MessageSid) via SmsWebhookController.
    /// Hidden from Swagger so production Try it out is not mistaken for a signed Twilio POST.
    /// </summary>
    [ApiExplorerSettings(IgnoreApi = true)]
    [HttpPost("incoming")]
    [Consumes("application/json")]
    public async Task<IActionResult> Incoming([FromBody] SMSRequest request)
    {
        if (!IsInternalSimulatorAllowed())
        {
            return Unauthorized(new
            {
                error = "JSON POST /api/sms/incoming is the Development simulator and is disabled in production. Live Twilio must HTTP POST application/x-www-form-urlencoded to /api/sms/incoming (From, To, Body, MessageSid). Sending SMS from the Twilio Console does not test live inbound SMS."
            });
        }

        if (request == null || string.IsNullOrWhiteSpace(request.Phone) || string.IsNullOrWhiteSpace(request.Message))
        {
            return BadRequest(new { error = "Phone and message are required." });
        }

        var result = await _processor.ProcessAsync(
            new IncomingSmsMessage
            {
                From = request.Phone,
                To = _options.BusinessPhoneNumber,
                Body = request.Message,
                Provider = "Internal"
            },
            sendReplyViaProvider: false);

        return Ok(new
        {
            customer = result.Phone,
            received = result.Received,
            reply = result.Reply,
            autoReplied = result.AutoReplied,
            humanOverride = result.HumanOverride,
            ownerNumberIgnored = result.OwnerNumberIgnored,
            ownerQuery = result.OwnerQueryHandled,
            intent = result.Intent,
            persisted = result.Persisted,
            fellBack = result.AiFellBack
        });
    }

    [ApiExplorerSettings(IgnoreApi = true)]
    [HttpGet("simulated")]
    public IActionResult SimulatedOutbox()
    {
        if (!_environment.IsDevelopment())
        {
            return NotFound();
        }

        return Ok(_simulated.Sent);
    }

    private bool IsInternalSimulatorAllowed()
    {
        if (!_environment.IsDevelopment())
        {
            return false;
        }

        if (!_options.AllowInternalSimulator)
        {
            return false;
        }

        if (_environment.IsProduction() && string.IsNullOrWhiteSpace(_options.InternalApiKey))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(_options.InternalApiKey))
        {
            if (!Request.Headers.TryGetValue("X-Reign-Internal-Key", out var provided))
            {
                return false;
            }

            return string.Equals(provided.ToString(), _options.InternalApiKey, StringComparison.Ordinal);
        }

        return true;
    }
}

public class SMSRequest
{
    public string Phone { get; set; } = "";

    public string Message { get; set; } = "";
}
