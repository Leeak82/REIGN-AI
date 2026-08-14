using Microsoft.EntityFrameworkCore;
using REIGN.Core.Catalog;
using REIGN.Data.Models;

namespace REIGN.Data.Seed;

public static class ServiceCatalogBootstrapper
{
    private static readonly string[] AutomotiveNameFragments =
    [
        "oil",
        "brake",
        "diagnostic",
        "vehicle inspection",
        "mechanic",
        "tire",
        "transmission"
    ];

    public static async Task EnsureAsync(ReignDbContext db, CancellationToken cancellationToken = default)
    {
        await UpsertServiceAsync(
            db,
            ServiceCatalog.QuickVisitId,
            ServiceCatalog.QuickVisitName,
            ServiceCatalog.QuickVisitPrice,
            ServiceCatalog.QuickVisitMinutes,
            cancellationToken);

        await UpsertServiceAsync(
            db,
            ServiceCatalog.HalfHourId,
            ServiceCatalog.HalfHourName,
            ServiceCatalog.HalfHourPrice,
            ServiceCatalog.HalfHourMinutes,
            cancellationToken);

        await UpsertServiceAsync(
            db,
            ServiceCatalog.HourId,
            ServiceCatalog.HourName,
            ServiceCatalog.HourPrice,
            ServiceCatalog.HourMinutes,
            cancellationToken);

        var services = await db.Services.ToListAsync(cancellationToken);
        foreach (var service in services)
        {
            if (IsAutomotive(service.Name) &&
                service.Id != ServiceCatalog.QuickVisitId &&
                service.Id != ServiceCatalog.HalfHourId &&
                service.Id != ServiceCatalog.HourId)
            {
                service.Active = false;
            }
        }

        var recommendations = await db.ServiceRecommendations.ToListAsync(cancellationToken);
        foreach (var recommendation in recommendations)
        {
            if (IsAutomotive(recommendation.Trigger) || IsAutomotive(recommendation.Recommendation))
            {
                recommendation.Active = false;
            }
        }

        await UpsertRecommendationAsync(
            db,
            ServiceCatalog.QuickVisitRecommendationId,
            "quick",
            "Customer is asking about a Quick Visit (QV): $150, less than 30 minutes.",
            ServiceCatalog.QuickVisitId,
            cancellationToken);

        await UpsertRecommendationAsync(
            db,
            ServiceCatalog.HalfHourRecommendationId,
            "half",
            "Customer is asking about a Half Hour appointment (HH): $300, 30 minutes.",
            ServiceCatalog.HalfHourId,
            cancellationToken);

        await UpsertRecommendationAsync(
            db,
            ServiceCatalog.HourRecommendationId,
            "hour",
            "Customer is asking about an Hour appointment (HR): $500, 60 minutes.",
            ServiceCatalog.HourId,
            cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
    }

    private static bool IsAutomotive(string value)
    {
        var text = value.ToLowerInvariant();
        return AutomotiveNameFragments.Any(fragment => text.Contains(fragment));
    }

    private static async Task UpsertServiceAsync(
        ReignDbContext db,
        Guid id,
        string name,
        decimal price,
        int durationMinutes,
        CancellationToken cancellationToken)
    {
        var existing = await db.Services.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? await db.Services.FirstOrDefaultAsync(x => x.Name == name, cancellationToken);

        if (existing == null)
        {
            db.Services.Add(new Service
            {
                Id = id,
                Name = name,
                Price = price,
                DurationMinutes = durationMinutes,
                Active = true
            });
            return;
        }

        existing.Name = name;
        existing.Price = price;
        existing.DurationMinutes = durationMinutes;
        existing.Active = true;
    }

    private static async Task UpsertRecommendationAsync(
        ReignDbContext db,
        Guid id,
        string trigger,
        string recommendation,
        Guid serviceId,
        CancellationToken cancellationToken)
    {
        var existing = await db.ServiceRecommendations.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? await db.ServiceRecommendations.FirstOrDefaultAsync(x => x.Trigger == trigger, cancellationToken);

        if (existing == null)
        {
            db.ServiceRecommendations.Add(new ServiceRecommendation
            {
                Id = id,
                Trigger = trigger,
                Recommendation = recommendation,
                ServiceId = serviceId,
                Active = true
            });
            return;
        }

        existing.Trigger = trigger;
        existing.Recommendation = recommendation;
        existing.ServiceId = serviceId;
        existing.Active = true;
    }
}
