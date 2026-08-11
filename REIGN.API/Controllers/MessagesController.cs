using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using REIGN.Data;
using REIGN.Data.Models;

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


    [HttpPost("send")]
    public async Task<IActionResult> SendMessage(SendMessageRequest request)
    {
        var customer = await _db.Customers
            .FirstOrDefaultAsync(x => x.PhoneNumber == request.PhoneNumber);


        if (customer == null)
            return NotFound();


        var message = new ConversationMessage
        {
            Id = Guid.NewGuid(),
            CustomerId = customer.Id,
            Direction = "Outbound",
            Body = request.Body,
            CreatedAt = DateTime.UtcNow
        };


        _db.ConversationMessages.Add(message);

        await _db.SaveChangesAsync();


        return Ok(message);
    }
}


public class SendMessageRequest
{
    public string PhoneNumber { get; set; } = "";

    public string Body { get; set; } = "";
}