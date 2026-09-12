using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using REIGN.API.Services;
using REIGN.Data;
using REIGN.Data.Models;

namespace REIGN.API.Controllers;

[ApiController]
[Route("api/services")]
public class ServiceCatalogController : ControllerBase
{
    private readonly CatalogIntelligence _catalog;
    private readonly ReignDbContext _db;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ServiceCatalogController> _logger;

    public ServiceCatalogController(
        CatalogIntelligence catalog,
        ReignDbContext db,
        IConfiguration configuration,
        ILogger<ServiceCatalogController> logger)
    {
        _catalog = catalog;
        _db = db;
        _configuration = configuration;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Get() => Ok(await _catalog.GetCatalogAsync());

    [HttpGet("manage")]
    public async Task<IActionResult> Manage()
    {
        var auth = RequireAdmin();
        if (auth != null)
        {
            return auth;
        }

        var services = await _db.Services
            .AsNoTracking()
            .OrderByDescending(x => x.Active)
            .ThenBy(x => x.Price)
            .ThenBy(x => x.Name)
            .Select(x => new
            {
                x.Id,
                x.Name,
                x.Price,
                x.DurationMinutes,
                x.Active
            })
            .ToListAsync();

        return Ok(new
        {
            summary = await _catalog.GetSummaryAsync(),
            services
        });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] ServiceEditRequest? request)
    {
        var auth = RequireAdmin();
        if (auth != null)
        {
            return auth;
        }

        var validation = Validate(request);
        if (validation != null)
        {
            return BadRequest(new { error = validation });
        }

        var name = request!.Name.Trim();
        if (await _db.Services.AnyAsync(x => x.Name.ToLower() == name.ToLower()))
        {
            return Conflict(new { error = "A service with that name already exists." });
        }

        var businessId = await _db.Businesses
            .Where(x => x.Active)
            .OrderBy(x => x.CreatedAt)
            .Select(x => (Guid?)x.Id)
            .FirstOrDefaultAsync();

        var service = new Service
        {
            BusinessId = businessId,
            Name = name,
            Price = request.Price,
            DurationMinutes = request.DurationMinutes,
            Active = request.Active
        };

        _db.Services.Add(service);
        await _db.SaveChangesAsync();
        return Ok(ToDto(service));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] ServiceEditRequest? request)
    {
        var auth = RequireAdmin();
        if (auth != null)
        {
            return auth;
        }

        var validation = Validate(request);
        if (validation != null)
        {
            return BadRequest(new { error = validation });
        }

        var service = await _db.Services.FirstOrDefaultAsync(x => x.Id == id);
        if (service == null)
        {
            return NotFound(new { error = "Service not found." });
        }

        var name = request!.Name.Trim();
        if (await _db.Services.AnyAsync(x => x.Id != id && x.Name.ToLower() == name.ToLower()))
        {
            return Conflict(new { error = "A service with that name already exists." });
        }

        service.Name = name;
        service.Price = request.Price;
        service.DurationMinutes = request.DurationMinutes;
        service.Active = request.Active;
        await _db.SaveChangesAsync();

        return Ok(ToDto(service));
    }

    [HttpPost("recommend")]
    public async Task<IActionResult> Recommend([FromBody] CatalogRecommendRequest? request)
    {
        if (string.IsNullOrWhiteSpace(request?.Message))
        {
            return BadRequest(new { error = "Message is required." });
        }

        try
        {
            return Ok(await _catalog.RecommendAsync(request.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Catalog recommendation failed.");
            return StatusCode(500, new { error = "Unable to recommend a service right now." });
        }
    }

    private IActionResult? RequireAdmin()
    {
        var configured = _configuration["REIGN_ADMIN_API_KEY"];
        if (string.IsNullOrWhiteSpace(configured))
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = "Service management is not configured." });
        }

        var supplied = Request.Headers["X-REIGN-ADMIN-KEY"].ToString();
        if (!SecureEquals(configured, supplied))
        {
            return Unauthorized(new { error = "Admin authorization is required." });
        }

        return null;
    }

    private static bool SecureEquals(string expected, string supplied)
    {
        var left = Encoding.UTF8.GetBytes(expected);
        var right = Encoding.UTF8.GetBytes(supplied ?? "");
        return left.Length == right.Length && CryptographicOperations.FixedTimeEquals(left, right);
    }

    private static string? Validate(ServiceEditRequest? request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Name))
        {
            return "Service name is required.";
        }

        if (request.Name.Trim().Length > 80)
        {
            return "Service name must be 80 characters or fewer.";
        }

        if (request.Price < 0 || request.Price > 100000)
        {
            return "Price must be between $0 and $100,000.";
        }

        if (request.DurationMinutes is < 5 or > 1440)
        {
            return "Duration must be between 5 and 1,440 minutes.";
        }

        return null;
    }

    private static object ToDto(Service service) => new
    {
        service.Id,
        service.Name,
        service.Price,
        service.DurationMinutes,
        service.Active
    };
}

public class CatalogRecommendRequest
{
    public string Message { get; set; } = "";
}

public class ServiceEditRequest
{
    public string Name { get; set; } = "";

    public decimal Price { get; set; }

    public int DurationMinutes { get; set; }

    public bool Active { get; set; } = true;
}
