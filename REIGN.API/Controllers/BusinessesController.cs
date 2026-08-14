using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using REIGN.API.Options;
using REIGN.Core.Catalog;

namespace REIGN.API.Controllers;

[ApiController]
[Route("api/businesses")]
public class BusinessesController : ControllerBase
{
    private readonly BusinessProfileOptions _business;

    public BusinessesController(IOptions<BusinessProfileOptions> business)
    {
        _business = business.Value;
    }

    [HttpGet]
    public IActionResult Get()
    {
        return Ok(new[]
        {
            new
            {
                name = _business.Name,
                assistant = _business.AssistantName,
                offering = _business.Offering,
                hours = _business.Hours,
                timeZone = _business.TimeZone,
                catalog = ServiceCatalog.CatalogSummary
            }
        });
    }
}
