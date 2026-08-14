using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using REIGN.API.Messaging;
using REIGN.API.Services;
using REIGN.Data;

namespace REIGN.API.Controllers;

[ApiController]
[Route("api/messages")]
public class MessagesController : ControllerBase
{
    private readonly ReignDbContext _db;
    private readonly OwnerMessagingService _ownerMessaging;
    private readonly ILogger<MessagesController> _logger;

    public MessagesController(
        ReignDbContext db,
        OwnerMessagingService ownerMessaging,
        ILogger<MessagesController> logger)
    {
        _db = db;
        _ownerMessaging = ownerMessaging;
        _logger = logger;
    }

    [HttpGet("{phone}")]
    public async Task<IActionResult> GetMessages(string phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
        {
            return BadRequest(new { error = "Phone is required." });
        }

        var normalized = PhoneNumbers.Normalize(phone);
        var messages = await _db.ConversationMessages
            .Include(x => x.Customer)
            .Where(x => x.Customer.PhoneNumber == normalized || x.Customer.PhoneNumber == phone)
            .OrderBy(x => x.CreatedAt)
            .Select(x => new
            {
                x.Id,
                Customer = x.Customer.Name ?? x.Customer.PhoneNumber,
                x.Direction,
                x.Body,
                x.Source,
                x.IsOwnerOverride,
                x.CreatedAt
            })
            .ToListAsync();

        return Ok(messages);
    }

    [HttpPost("send")]
    public async Task<IActionResult> SendMessage(SendMessageRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.PhoneNumber) || string.IsNullOrWhiteSpace(request.Body))
        {
            return BadRequest(new { error = "PhoneNumber and Body are required." });
        }

        var result = await _ownerMessaging.SendOverrideAsync(request.PhoneNumber, request.Body);
        if (!result.Succeeded && result.Error == "Customer not found.")
        {
            return NotFound(new { error = result.Error });
        }

        if (!result.Succeeded)
        {
            _logger.LogWarning("Owner outbound SMS failed for {Phone}: {Error}", request.PhoneNumber, result.Error);
        }

        return Ok(new
        {
            sent = result.Succeeded,
            humanOverride = result.HumanOverrideActive,
            simulated = result.Outbound?.Simulated ?? false,
            provider = result.Outbound?.Provider,
            error = result.Error
        });
    }

    [HttpPost("resume")]
    public async Task<IActionResult> Resume(SendMessageRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.PhoneNumber))
        {
            return BadRequest(new { error = "PhoneNumber is required." });
        }

        var resumed = await _ownerMessaging.ResumeAssistantAsync(request.PhoneNumber);
        if (!resumed)
        {
            return NotFound(new { error = "Customer not found." });
        }

        return Ok(new { humanOverride = false });
    }
}

public class SendMessageRequest
{
    public string PhoneNumber { get; set; } = "";

    public string Body { get; set; } = "";
}
