using Microsoft.AspNetCore.Mvc;

namespace REIGN.API.Controllers;

[ApiController]
[Route("api/health")]
public class HealthController : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        return Ok(new
        {
            Status = "REIGN API Online",
            Time = DateTime.UtcNow
        });
    }
}
