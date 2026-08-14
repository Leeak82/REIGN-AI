using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using REIGN.Data;

namespace REIGN.API.Controllers;

[ApiController]
[Route("api/messages")]
public class MessagesController : ControllerBase
{
    private readonly ReignDbContext _db;

    public MessagesController(ReignDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetAllMessages()
    {
        var messages = await _db.ConversationMessages
            .Include(x => x.Customer)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new
            {
                x.Id,
                Customer = x.Customer.Name ?? x.Customer.PhoneNumber,
                Phone = x.Customer.PhoneNumber,
                x.Direction,
                x.Body,
                x.CreatedAt
            })
            .ToListAsync();

        return Ok(messages);
    }

    [HttpGet("{phone}")]
    public async Task<IActionResult> GetMessages(string phone)
    {
        var messages = await _db.ConversationMessages
            .Include(x => x.Customer)
            .Where(x => x.Customer.PhoneNumber == phone)
            .OrderBy(x => x.CreatedAt)
            .Select(x => new
            {
                x.Id,
                Customer = x.Customer.Name ?? x.Customer.PhoneNumber,
                x.Direction,
                x.Body,
                x.CreatedAt
            })
            .ToListAsync();

        return Ok(messages);
    }
}
