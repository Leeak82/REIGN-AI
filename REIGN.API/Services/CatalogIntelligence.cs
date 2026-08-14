using Microsoft.EntityFrameworkCore;
using REIGN.Core.Catalog;
using REIGN.Data;

namespace REIGN.API.Services;

public class CatalogIntelligence
{
    private readonly ReignDbContext _db;

    public CatalogIntelligence(ReignDbContext db)
    {
        _db = db;
    }

    public async Task<object> GetCatalogAsync()
    {
        var rows = await _db.Services
            .Where(x => x.Active)
            .OrderBy(x => x.Price)
            .ToListAsync();

        var services = rows.Select(x => new
        {
            x.Id,
            x.Name,
            x.Price,
            x.DurationMinutes,
            Code = x.Name == ServiceCatalog.QuickVisitName ? ServiceCatalog.QuickVisitCode
                : x.Name == ServiceCatalog.HalfHourName ? ServiceCatalog.HalfHourCode
                : x.Name == ServiceCatalog.HourName ? ServiceCatalog.HourCode
                : x.Name
        });

        var recommendations = await _db.ServiceRecommendations
            .Include(x => x.Service)
            .Where(x => x.Active)
            .ToListAsync();

        return new
        {
            summary = ServiceCatalog.CatalogSummary,
            services,
            recommendations = recommendations
                .Where(x => x.Service != null)
                .Select(x => new
                {
                    x.Trigger,
                    x.Recommendation,
                    Service = x.Service!.Name,
                    Price = x.Service.Price,
                    DurationMinutes = x.Service.DurationMinutes
                })
        };
    }

    public async Task<object> RecommendAsync(string message)
    {
        var text = (message ?? "").ToLowerInvariant();
        var matchedName = BookingService.MatchCatalogService(text);

        var candidates = await _db.ServiceRecommendations
            .Include(x => x.Service)
            .Where(x => x.Active)
            .ToListAsync();

        var match = candidates
            .Where(x => x.Service != null)
            .OrderByDescending(x => x.Trigger.Length)
            .FirstOrDefault(x =>
                text.Contains(x.Trigger) ||
                (!string.IsNullOrWhiteSpace(matchedName) && x.Service!.Name == matchedName));

        if (match?.Service == null)
        {
            return new
            {
                service = "Unknown",
                recommendation = $"Ask which session they want. {ServiceCatalog.CatalogSummary}."
            };
        }

        return new
        {
            service = match.Service.Name,
            price = match.Service.Price,
            duration = match.Service.DurationMinutes,
            recommendation = match.Recommendation
        };
    }
}
