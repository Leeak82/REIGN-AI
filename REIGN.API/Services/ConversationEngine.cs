using Microsoft.EntityFrameworkCore;
using REIGN.Data;
using REIGN.Data.Models;

namespace REIGN.API.Services;

public class ConversationEngine
{
    private readonly ReignDbContext _db;


    public ConversationEngine(ReignDbContext db)
    {
        _db = db;
    }


    public async Task<string> Process(
        Customer customer,
        string message)
    {

        message = message.Trim();


        // Capture customer name
        if (string.IsNullOrWhiteSpace(customer.Name))
        {
            if(message.Length > 2 &&
               !message.Contains("oil", StringComparison.OrdinalIgnoreCase) &&
               !message.Contains("change", StringComparison.OrdinalIgnoreCase))
            {
                customer.Name = message;

                await _db.SaveChangesAsync();

                return $"Thanks {customer.Name}. I saved your information. How can I help you today?";
            }

            return "I'd be happy to help. May I get your name first?";
        }


        // Existing customer flow

        if(message.Contains("oil", StringComparison.OrdinalIgnoreCase))
        {
            return $"Thanks {customer.Name}. I can schedule your Oil Change. What day and time works best?";
        }


        return $"Hi {customer.Name}, how can I help you today?";
    }
}
