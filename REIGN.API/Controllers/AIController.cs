using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using REIGN.API.Options;
using REIGN.API.Services;
using REIGN.Core.AI;
using REIGN.Data;

namespace REIGN.API.Controllers;

[ApiController]
[Route("api/ai")]
public class AIController : ControllerBase
{
    private readonly ReignDbContext _db;
    private readonly IncomingSmsProcessor _processor;
    private readonly OwnerAssistantService _ownerAssistant;
    private readonly IAiProvider _ai;
    private readonly AiOptions _aiOptions;

    public AIController(
        ReignDbContext db,
        IncomingSmsProcessor processor,
        OwnerAssistantService ownerAssistant,
        IAiProvider ai,
        IOptions<AiOptions> aiOptions)
    {
        _db = db;
        _processor = processor;
        _ownerAssistant = ownerAssistant;
        _ai = ai;
        _aiOptions = aiOptions.Value;
    }

    [HttpGet("status")]
    public IActionResult Status()
    {
        return Ok(new
        {
            provider = _ai.ProviderName,
            groqConfigured = !string.IsNullOrWhiteSpace(_aiOptions.ApiKey),
            model = _aiOptions.Model,
            fallbackAvailable = true
        });
    }

    [HttpPost("chat")]
    public async Task<IActionResult> Chat([FromBody] ChatRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Phone) || string.IsNullOrWhiteSpace(request.Message))
        {
            return BadRequest(new { error = "Phone and message are required." });
        }

        var result = await _processor.ProcessAsync(new Messaging.IncomingSmsMessage
        {
            From = request.Phone,
            Body = request.Message,
            Provider = "AI"
        }, sendReplyViaProvider: false);

        return Ok(new
        {
            customer = result.Phone,
            received = result.Received,
            reply = result.Reply,
            intent = result.Intent,
            autoReplied = result.AutoReplied,
            humanOverride = result.HumanOverride,
            ownerQuery = result.OwnerQueryHandled,
            persisted = result.Persisted,
            fellBack = result.AiFellBack
        });
    }

    [HttpPost("owner")]
    public async Task<IActionResult> Owner([FromBody] OwnerRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
        {
            return BadRequest(new { error = "Message is required." });
        }

        var reply = await _ownerAssistant.AnswerAsync(request.Message);
        return Ok(new { reply, intent = "owner_activity" });
    }

    [HttpPost("recommend")]
    public async Task<IActionResult> Recommend([FromBody] AIRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
        {
            return BadRequest(new { error = "Message is required." });
        }

        var message = request.Message.ToLowerInvariant();

        var match =
            await _db.ServiceRecommendations
            .Include(x => x.Service)
            .Where(x => x.Active)
            .OrderByDescending(x => x.Trigger.Length)
            .FirstOrDefaultAsync(x =>
                message.Contains(x.Trigger));

        if (match == null)
        {
            return Ok(new
            {
                service = "Unknown",
                recommendation =
                "Ask additional questions to determine customer needs."
            });
        }

        return Ok(new
        {
            service = match.Service?.Name ?? "Unknown",
            price = match.Service?.Price,
            duration = match.Service?.DurationMinutes,
            recommendation = match.Recommendation
        });
    }

    public class AIRequest
    {
        public string Message { get; set; } = "";
    }

    public class ChatRequest
    {
        public string Phone { get; set; } = "";

        public string Message { get; set; } = "";
    }

    public class OwnerRequest
    {
        public string Message { get; set; } = "";
    }
}
