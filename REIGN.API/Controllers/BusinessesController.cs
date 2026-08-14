using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using REIGN.Data;

namespace REIGN.API.Controllers;

[ApiController]
[Route("api/businesses")]
public class BusinessesController : ControllerBase
{
    private readonly ReignDbContext _db;

    public BusinessesController(ReignDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var businesses = await _db.Businesses.ToListAsync();
        return Ok(businesses);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(Guid id)
    {
        var business = await _db.Businesses
            .FirstOrDefaultAsync(x => x.Id == id);

        if (business == null)
            return NotFound();

        return Ok(business);
    }
}
