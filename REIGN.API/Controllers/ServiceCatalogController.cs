using Microsoft.AspNetCore.Mvc;
using REIGN.API.Services;

namespace REIGN.API.Controllers;

[ApiController]
[Route("api/services")]
public class ServiceCatalogController : ControllerBase
{
    private readonly CatalogIntelligence _catalog;
    private readonly ILogger<ServiceCatalogController> _logger;

    public ServiceCatalogController(CatalogIntelligence catalog, ILogger<ServiceCatalogController> logger)
    {
        _catalog = catalog;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Get() => Ok(await _catalog.GetCatalogAsync());

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
}

public class CatalogRecommendRequest
{
    public string Message { get; set; } = "";
}
