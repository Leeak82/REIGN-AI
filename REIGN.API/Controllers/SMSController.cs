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
    /// Internal simulator / application endpoint. This is not a provider webhook.
    /// Provider inbound traffic must use /api/sms/webhooks/twilio or /api/sms/webhooks/vonage.
    /// </summary>
    [HttpPost("incoming")]
    public async Task<IActionResult> Incoming([FromBody] SMSRequest request)
    {
        if (!IsInternalSimulatorAllowed())
        {
            return Unauthorized(new { error = "Internal SMS simulator is disabled or missing X-Reign-Internal-Key." });
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

    [HttpGet("simulated")]
    public IActionResult SimulatedOutbox()
    {
        if (!_environment.IsDevelopment() && !_options.Provider.Equals("Simulated", StringComparison.OrdinalIgnoreCase))
        {
            return NotFound();
        }

        return Ok(_simulated.Sent);
    }

    private bool IsInternalSimulatorAllowed()
    {
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
