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
                .ThenInclude(x => x.Service)
            .Include(x => x.Messages)
            .FirstOrDefaultAsync(x => x.Id == customerId);



        if (customer == null)
            return "";



        var lastAppointment =
            customer.Appointments
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefault();



        var completedServices =
            customer.Appointments
            .Where(x => x.Service != null)
            .Select(x => x.Service!.Name)
            .Distinct()
            .ToList();



        var state =
            await _db.ConversationStates
            .FirstOrDefaultAsync(x =>
                x.CustomerId == customerId);



        if (lastAppointment == null)
        {
            return
                $"New customer: {customer.Name ?? "Unknown"}. " +
                $"Messages: {customer.Messages.Count}.";
        }



        return
            $"Customer: {customer.Name ?? "Unknown"}. " +
            $"Phone: {customer.PhoneNumber}. " +
            $"Messages: {customer.Messages.Count}. " +
            $"Appointments: {customer.Appointments.Count}. " +
            $"Services used: {string.Join(", ", completedServices)}. " +
            $"Last service: {lastAppointment.Service?.Name ?? "Unknown"}. " +
            $"Last appointment: {lastAppointment.AppointmentTime:g}. " +
            $"Status: {lastAppointment.Status}. " +
            $"Current step: {state?.CurrentStep ?? "None"}.";
    }
}
