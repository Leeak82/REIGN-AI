using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
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
    private readonly IConfiguration _configuration;

    public BusinessesController(ReignDbContext db, IBusinessProfileAccessor profiles)
        : this(db, profiles, new ConfigurationBuilder().Build())
    {
    }

    public BusinessesController(
        ReignDbContext db,
        IBusinessProfileAccessor profiles,
        IConfiguration configuration)
    {
        _db = db;
        _profiles = profiles;
        _configuration = configuration;
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
                x.Address,
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
            address = x.Address,
            hours = x.Hours,
            timeZone = x.TimeZone,
            assistant = profile.Id == x.Id ? profile.AssistantName : "REIGN",
            offering = profile.Id == x.Id ? profile.Offering : "",
            catalog = ServiceCatalog.CatalogSummary,
            active = x.Active
        }));
    }

    [HttpGet("manage")]
    public async Task<IActionResult> Manage()
    {
        var auth = RequireAdmin();
        if (auth != null) return auth;

        var business = await _db.Businesses
            .AsNoTracking()
            .Where(x => x.Active)
            .OrderBy(x => x.CreatedAt)
            .FirstOrDefaultAsync()
            ?? await _db.Businesses.AsNoTracking().OrderBy(x => x.CreatedAt).FirstOrDefaultAsync();

        if (business == null)
        {
            return NotFound(new { error = "Business profile not found." });
        }

        return Ok(ToAdminDto(business));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] BusinessEditRequest? request)
    {
        var auth = RequireAdmin();
        if (auth != null) return auth;

        if (request == null || string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(new { error = "Business name is required." });
        }

        if (request.Name.Trim().Length > 120 ||
            (request.OwnerName?.Length ?? 0) > 120 ||
            (request.Phone?.Length ?? 0) > 40 ||
            (request.Email?.Length ?? 0) > 200 ||
            (request.Address?.Length ?? 0) > 300 ||
            (request.Hours?.Length ?? 0) > 500 ||
            (request.TimeZone?.Length ?? 0) > 100)
        {
            return BadRequest(new { error = "One or more business profile fields are too long." });
        }

        var business = await _db.Businesses.FirstOrDefaultAsync(x => x.Id == id);
        if (business == null)
        {
            return NotFound(new { error = "Business profile not found." });
        }

        business.Name = request.Name.Trim();
        business.OwnerName = request.OwnerName?.Trim() ?? "";
        business.Phone = request.Phone?.Trim() ?? "";
        business.Email = request.Email?.Trim() ?? "";
        business.Address = request.Address?.Trim() ?? "";
        business.Hours = string.IsNullOrWhiteSpace(request.Hours)
            ? "By appointment, typically 9am–5pm."
            : request.Hours.Trim();
        business.TimeZone = string.IsNullOrWhiteSpace(request.TimeZone)
            ? "America/Los_Angeles"
            : request.TimeZone.Trim();

        await _db.SaveChangesAsync();
        return Ok(ToAdminDto(business));
    }

    private IActionResult? RequireAdmin()
    {
        var configured = _configuration["REIGN_ADMIN_API_KEY"];
        if (string.IsNullOrWhiteSpace(configured))
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = "Business profile editing is not configured." });
        }

        var supplied = Request.Headers["X-REIGN-ADMIN-KEY"].ToString();
        var left = Encoding.UTF8.GetBytes(configured);
        var right = Encoding.UTF8.GetBytes(supplied ?? "");
        if (left.Length != right.Length || !CryptographicOperations.FixedTimeEquals(left, right))
        {
            return Unauthorized(new { error = "Admin authorization is required." });
        }

        return null;
    }

    private static object ToAdminDto(REIGN.Data.Models.Business business) => new
    {
        business.Id,
        business.Name,
        business.OwnerName,
        business.Phone,
        business.Email,
        business.Address,
        business.Hours,
        business.TimeZone,
        business.Active
    };
}

public sealed class BusinessEditRequest
{
    public string Name { get; set; } = "";

    public string? OwnerName { get; set; }

    public string? Phone { get; set; }

    public string? Email { get; set; }

    public string? Address { get; set; }

    public string? Hours { get; set; }

    public string? TimeZone { get; set; }
}
