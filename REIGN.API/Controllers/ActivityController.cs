using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using REIGN.API.Services;
using REIGN.Data;

namespace REIGN.API.Controllers;

[ApiController]
[Route("api/activity")]
public class ActivityController : ControllerBase
{
    private readonly OwnerAssistantService _ownerAssistant;
    private readonly ReignDbContext _db;
    private readonly ILogger<ActivityController> _logger;

    public ActivityController(
        OwnerAssistantService ownerAssistant,
        ReignDbContext db,
        ILogger<ActivityController> logger)
    {
        _ownerAssistant = ownerAssistant;
        _db = db;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var snapshot = await _ownerAssistant.BuildSnapshotAsync();
        return Ok(new { snapshot, intent = "owner_activity" });
    }

    [HttpGet("recent")]
    public async Task<IActionResult> GetRecent()
    {
        var activity =
            await _db.ConversationMessages
                .Include(x => x.Customer)
                .OrderByDescending(x => x.CreatedAt)
                .Take(25)
                .Select(x => new
                {
                    Time = x.CreatedAt,
                    Customer = x.Customer.Name ?? x.Customer.PhoneNumber,
                    Direction = x.Direction,
                    Message = x.Body
                })
                .ToListAsync();

        return Ok(activity);
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
