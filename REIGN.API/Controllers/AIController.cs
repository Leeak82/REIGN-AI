using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using REIGN.Data;

namespace REIGN.API.Controllers;

[ApiController]
[Route("api/ai")]
public class AIController : ControllerBase
{

    private readonly ReignDbContext _db;


    public AIController(ReignDbContext db)
    {
        _db = db;
    }



    [HttpPost("recommend")]
    public async Task<IActionResult> Recommend([FromBody] AIRequest request)
    {

        var message = request.Message.ToLower();


        var match =
            await _db.ServiceRecommendations
            .Include(x => x.Service)
            .Where(x => x.Active)
            .FirstOrDefaultAsync(x =>
                message.Contains(x.Trigger));


        if(match == null)
        {
            return Ok(new
            {
                service = "Unknown",
                recommendation =
                "Ask additional questions to determine customer needs."
            });
        }



        return Ok(new
        {
            service = match.Service?.Name ?? "Unknown",

            price = match.Service?.Price,

            duration = match.Service?.DurationMinutes,

            recommendation = match.Recommendation
        });

    }



    public class AIRequest
    {
        public string Message {get;set;}="";
    }

}
