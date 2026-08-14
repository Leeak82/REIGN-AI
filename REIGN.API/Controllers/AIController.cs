using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using REIGN.API.Options;
using REIGN.API.Services;
using REIGN.Core.AI;

namespace REIGN.API.Controllers;

[ApiController]
[Route("api/ai")]
public class AIController : ControllerBase
{
    private readonly IncomingSmsProcessor _processor;
    private readonly OwnerAssistantService _ownerAssistant;
    private readonly CatalogIntelligence _catalog;
    private readonly IAiProvider _ai;
    private readonly AiOptions _aiOptions;
    private readonly ILogger<AIController> _logger;

    public AIController(
        IncomingSmsProcessor processor,
        OwnerAssistantService ownerAssistant,
        CatalogIntelligence catalog,
        IAiProvider ai,
        IOptions<AiOptions> aiOptions,
        ILogger<AIController> logger)
    {
        _processor = processor;
        _ownerAssistant = ownerAssistant;
        _catalog = catalog;
        _ai = ai;
        _aiOptions = aiOptions.Value;
        _logger = logger;
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

        try
        {
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
        catch (Exception ex)
        {
            _logger.LogError(ex, "AI chat failed for {Phone}", request.Phone);
            return StatusCode(500, new { error = "Unable to process that conversation right now." });
        }
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

        return Ok(await _catalog.RecommendAsync(request.Message));
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
