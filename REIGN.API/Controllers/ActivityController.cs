using Microsoft.AspNetCore.Mvc;
using REIGN.API.Services;

namespace REIGN.API.Controllers;

[ApiController]
[Route("api/activity")]
public class ActivityController : ControllerBase
{
    private readonly OwnerAssistantService _ownerAssistant;
    private readonly ILogger<ActivityController> _logger;

    public ActivityController(OwnerAssistantService ownerAssistant, ILogger<ActivityController> logger)
    {
        _ownerAssistant = ownerAssistant;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var snapshot = await _ownerAssistant.BuildSnapshotAsync();
        return Ok(new { snapshot, intent = "owner_activity" });
    }

    [HttpPost]
    public async Task<IActionResult> Ask([FromBody] ActivityRequest? request)
    {
        if (string.IsNullOrWhiteSpace(request?.Message))
        {
            return BadRequest(new { error = "Message is required." });
        }

        try
        {
            var reply = await _ownerAssistant.AnswerAsync(request.Message);
            return Ok(new { reply, intent = "owner_activity" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Owner activity query failed.");
            return StatusCode(500, new { error = "Unable to load activity right now." });
        }
    }
}

public class ActivityRequest
{
    public string Message { get; set; } = "";
}
