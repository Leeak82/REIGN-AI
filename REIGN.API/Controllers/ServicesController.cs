using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using REIGN.Data;

namespace REIGN.API.Controllers;

[ApiController]
[Route("api/services")]
public class ServicesController(ReignDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var services = await db.Services
            .Where(x => x.Active)
            .ToListAsync();

        return Ok(services);
    }
}
