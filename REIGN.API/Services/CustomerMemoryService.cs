using Microsoft.EntityFrameworkCore;
using REIGN.Data;

namespace REIGN.API.Services;

public class CustomerMemoryService
{
    private readonly ReignDbContext _db;


    public CustomerMemoryService(ReignDbContext db)
    {
        _db = db;
    }


    public async Task<string> GetCustomerContext(Guid customerId)
    {
        var customer = await _db.Customers
            .Include(x => x.Appointments)
            .Include(x => x.Messages)
            .FirstOrDefaultAsync(x => x.Id == customerId);


        if (customer == null)
            return "";


        var lastAppointment = customer.Appointments
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefault();


        if (lastAppointment == null)
        {
            return $"New customer {customer.Name ?? "Unknown"}";
        }


        return
            $"Returning customer: {customer.Name ?? "Unknown"}. " +
            $"Last service: {lastAppointment.Status}. " +
            $"Previous appointment: {lastAppointment.AppointmentTime}.";
    }
}
