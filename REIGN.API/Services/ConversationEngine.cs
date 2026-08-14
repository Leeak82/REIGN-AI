using REIGN.Core.Catalog;
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

    public async Task<string> Process(Customer customer, string message)
    {
        message = message.Trim();

        if (string.IsNullOrWhiteSpace(customer.Name))
        {
            var extracted = ConversationService.TryExtractName(message);
            if (!string.IsNullOrWhiteSpace(extracted))
            {
                customer.Name = extracted;
                await _db.SaveChangesAsync();
                return $"Thanks {customer.Name}. I saved your information. {ServiceCatalog.CatalogSummary}. Which would you like?";
            }

            return "I'd be happy to help. May I get your name first?";
        }

        var service = BookingService.MatchCatalogService(message);
        if (!string.IsNullOrWhiteSpace(service))
        {
            return $"Thanks {customer.Name}. I can schedule your {service}. What day and time works best?";
        }

        return $"Hi {customer.Name}, how can I help you today? I can book {ServiceCatalog.CatalogSummary}.";
    }
}
