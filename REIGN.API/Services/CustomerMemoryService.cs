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
        {
            return "";
        }

        var lastAppointment = customer.Appointments
            .Where(x => x.Status != "Cancelled")
            .OrderByDescending(x => x.AppointmentTime)
            .FirstOrDefault();

        var recent = customer.Messages
            .OrderByDescending(x => x.CreatedAt)
            .Take(6)
            .OrderBy(x => x.CreatedAt)
            .Select(x => $"{x.Direction}: {x.Body}")
            .ToList();

        if (lastAppointment == null && customer.TurnCount <= 1)
        {
            return $"New customer {customer.Name ?? customer.PhoneNumber}.";
        }

        var appointmentBit = lastAppointment == null
            ? "No prior appointments."
            : $"Last appointment: {lastAppointment.Service?.Name ?? "session"} on {lastAppointment.AppointmentTime:g} ({lastAppointment.Status}).";

        var historyBit = recent.Count == 0 ? "" : " Recent messages: " + string.Join(" | ", recent);

        var preferenceBit = string.IsNullOrWhiteSpace(customer.Notes)
            ? ""
            : $" Preferences: {customer.Notes}.";

        return
            $"Returning customer: {customer.Name ?? customer.PhoneNumber}. {appointmentBit} " +
            $"Pending service: {customer.PendingServiceName ?? "none"}.{preferenceBit}{historyBit}";
    }

    public async Task<List<(string Role, string Content)>> GetRecentTurns(Guid customerId, int take = 8)
    {
        var messages = await _db.ConversationMessages
            .Where(x => x.CustomerId == customerId)
            .OrderByDescending(x => x.CreatedAt)
            .Take(take)
            .ToListAsync();

        return messages
            .OrderBy(x => x.CreatedAt)
            .Select(x => (x.Direction == "Outbound" ? "Assistant" : "Customer", x.Body))
            .ToList();
    }
}
