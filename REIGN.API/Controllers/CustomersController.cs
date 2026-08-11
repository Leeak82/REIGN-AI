using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using REIGN.Data;

namespace REIGN.API.Controllers;

[ApiController]
[Route("api/customers")]
public class CustomersController : ControllerBase
{
    private readonly ReignDbContext _db;

    public CustomersController(ReignDbContext db)
    {
        _db = db;
    }


    [HttpGet]
    public async Task<IActionResult> GetCustomers()
    {
        var customers = await _db.Customers
            .Select(c => new
            {
                c.Id,
                c.PhoneNumber,
                c.Name,
                Messages = c.Messages.Count,
                Appointments = c.Appointments.Count
            })
            .ToListAsync();

        return Ok(customers);
    }
}
