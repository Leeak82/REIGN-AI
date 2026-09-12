using Microsoft.EntityFrameworkCore;
using REIGN.Core.Catalog;
using REIGN.Data;
using REIGN.Data.Models;

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
        var rows = await ActiveServicesAsync();
        var recommendations = await _db.ServiceRecommendations
            .Include(x => x.Service)
            .Where(x => x.Active && x.Service != null && x.Service.Active)
            .ToListAsync();

        return new
        {
            summary = BuildSummary(rows),
            services = rows.Select(x => new
            {
                x.Id,
                x.Name,
                x.Price,
                x.DurationMinutes,
                x.Active,
                Code = CodeFor(x.Name)
            }),
            recommendations = recommendations.Select(x => new
            {
                x.Trigger,
                x.Recommendation,
                Service = x.Service!.Name,
                Price = x.Service.Price,
                DurationMinutes = x.Service.DurationMinutes
            })
        };
    }

    public async Task<string> GetSummaryAsync()
    {
        return BuildSummary(await ActiveServicesAsync());
    }

    public async Task<string?> MatchServiceNameAsync(string message)
    {
        var text = (message ?? "").Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var services = await ActiveServicesAsync();
        var canonical = BookingService.MatchCatalogService(text);
        if (!string.IsNullOrWhiteSpace(canonical))
        {
            var activeCanonical = services.FirstOrDefault(x =>
                x.Name.Equals(canonical, StringComparison.OrdinalIgnoreCase));
            if (activeCanonical != null)
            {
                return activeCanonical.Name;
            }
        }

        return services
            .OrderByDescending(x => x.Name.Length)
            .FirstOrDefault(x => text.Contains(x.Name.ToLowerInvariant(), StringComparison.Ordinal))
            ?.Name;
    }

    public async Task<CatalogRecommendationResult> RecommendAsync(string message)
    {
        var text = (message ?? "").ToLowerInvariant();
        var matchedName = await MatchServiceNameAsync(text);

        var candidates = await _db.ServiceRecommendations
            .Include(x => x.Service)
            .Where(x => x.Active && x.Service != null && x.Service.Active)
            .ToListAsync();

        var match = candidates
            .OrderByDescending(x => x.Trigger.Length)
            .FirstOrDefault(x =>
                text.Contains(x.Trigger) ||
                (!string.IsNullOrWhiteSpace(matchedName) &&
                 x.Service!.Name.Equals(matchedName, StringComparison.OrdinalIgnoreCase)));

        if (match?.Service == null)
        {
            return new CatalogRecommendationResult
            {
                Service = "Unknown",
                Recommendation = $"Ask which service they want. {await GetSummaryAsync()}."
            };
        }

        return new CatalogRecommendationResult
        {
            Service = match.Service.Name,
            Price = match.Service.Price,
            Duration = match.Service.DurationMinutes,
            Recommendation = match.Recommendation
        };
    }

    private async Task<List<Service>> ActiveServicesAsync()
    {
        return await _db.Services
            .AsNoTracking()
            .Where(x => x.Active)
            .OrderBy(x => x.Price)
            .ThenBy(x => x.Name)
            .ToListAsync();
    }

    private static string BuildSummary(IReadOnlyCollection<Service> services)
    {
        if (services.Count == 0)
        {
            return "No active services are currently available.";
        }

        return string.Join(", ", services.Select(x =>
        {
            var code = CodeFor(x.Name);
            var prefix = code.Equals(x.Name, StringComparison.OrdinalIgnoreCase)
                ? x.Name
                : $"{code} {x.Name}";
            return $"{prefix} (${x.Price:0.##}, {x.DurationMinutes} minutes)";
        }));
    }

    private static string CodeFor(string name) => name switch
    {
        ServiceCatalog.QuickVisitName => ServiceCatalog.QuickVisitCode,
        ServiceCatalog.HalfHourName => ServiceCatalog.HalfHourCode,
        ServiceCatalog.HourName => ServiceCatalog.HourCode,
        _ => name
    };
}

public class CatalogRecommendationResult
{
    public string Service { get; set; } = "";

    public decimal? Price { get; set; }

    public int? Duration { get; set; }

    public string Recommendation { get; set; } = "";
}
