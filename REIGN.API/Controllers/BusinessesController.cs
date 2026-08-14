using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using REIGN.API.Services;
using REIGN.Core.Catalog;
using REIGN.Data;

namespace REIGN.API.Controllers;

[ApiController]
[Route("api/businesses")]
public class BusinessesController : ControllerBase
{
    private readonly ReignDbContext _db;
    private readonly IBusinessProfileAccessor _profiles;

    public BusinessesController(ReignDbContext db, IBusinessProfileAccessor profiles)
    {
        _db = db;
        _profiles = profiles;
    }

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var profile = await _profiles.GetActiveAsync();
        var businesses = await _db.Businesses
            .AsNoTracking()
            .Where(x => x.Active)
            .OrderBy(x => x.CreatedAt)
            .Select(x => new
            {
                x.Id,
                x.Name,
                x.OwnerName,
                x.Phone,
                x.Email,
                x.Hours,
                x.TimeZone,
                x.Active
            })
            .ToListAsync();

        if (businesses.Count == 0)
        {
            return Ok(new[]
            {
                new
                {
                    profile.Id,
                    name = profile.Name,
                    assistant = profile.AssistantName,
                    offering = profile.Offering,
                    hours = profile.Hours,
                    timeZone = profile.TimeZone,
                    catalog = ServiceCatalog.CatalogSummary
                }
            });
        }

        return Ok(businesses.Select(x => new
        {
            x.Id,
            name = x.Name,
            ownerName = x.OwnerName,
            phone = x.Phone,
            email = x.Email,
            hours = x.Hours,
            timeZone = x.TimeZone,
            assistant = profile.Id == x.Id ? profile.AssistantName : "REIGN",
            offering = profile.Id == x.Id ? profile.Offering : "",
            catalog = ServiceCatalog.CatalogSummary,
            active = x.Active
        }));
    }
}
