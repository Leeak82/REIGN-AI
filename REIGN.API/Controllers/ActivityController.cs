using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using REIGN.Data;

namespace REIGN.API.Controllers;

[ApiController]
[Route("api/activity")]
public class ActivityController : ControllerBase
{
    private readonly ReignDbContext _db;


    public ActivityController(ReignDbContext db)
    {
        _db = db;
    }



    [HttpGet]
    public async Task<IActionResult> Get()
    {

        var activity =
            await _db.ConversationMessages
            .Include(x => x.Customer)
            .OrderByDescending(x => x.CreatedAt)
            .Take(25)
            .Select(x => new
            {
                Time = x.CreatedAt,

                Customer =
                    x.Customer.Name ??
                    x.Customer.PhoneNumber,

                Direction =
                    x.Direction,

                Message =
                    x.Body
            })
            .ToListAsync();



        return Ok(activity);
    }
}
