using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using REIGN.Data;

namespace REIGN.API.Controllers;

[ApiController]
[Route("api/services")]
public class ServiceCatalogController : ControllerBase
{
    private readonly ReignDbContext _db;

    public ServiceCatalogController(ReignDbContext db)
    {
        _db = db;
    }


    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var services = await _db.Services
            .Where(x => x.Active)
            .Select(x => new
            {
                x.Id,
                x.Name,
                x.Description,
                x.Price,
                x.DurationMinutes,
                x.Active
            })
            .ToListAsync();

        return Ok(services);
    }


    [HttpGet("{id}")]
    public async Task<IActionResult> Get(Guid id)
    {
        var service = await _db.Services
            .Where(x => x.Id == id)
            .Select(x => new
            {
                x.Id,
                x.Name,
                x.Description,
                x.Price,
                x.DurationMinutes,
                x.Active
            })
            .FirstOrDefaultAsync();


        if (service == null)
            return NotFound();


        return Ok(service);
    }
}
