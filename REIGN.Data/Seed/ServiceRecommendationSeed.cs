using REIGN.Core.Catalog;
using REIGN.Data.Models;

namespace REIGN.Data.Seed;

public static class ServiceRecommendationSeed
{
    public static List<ServiceRecommendation> Get()
    {
        return
        [
            new ServiceRecommendation
            {
                Id = ServiceCatalog.QuickVisitRecommendationId,
                Trigger = "quick",
                Recommendation = "Customer is asking about a Quick Visit (QV): $150, less than 30 minutes.",
                ServiceId = ServiceCatalog.QuickVisitId,
                Active = true
            },
            new ServiceRecommendation
            {
                Id = ServiceCatalog.HalfHourRecommendationId,
                Trigger = "half",
                Recommendation = "Customer is asking about a Half Hour appointment (HH): $300, 30 minutes.",
                ServiceId = ServiceCatalog.HalfHourId,
                Active = true
            },
            new ServiceRecommendation
            {
                Id = ServiceCatalog.HourRecommendationId,
                Trigger = "hour",
                Recommendation = "Customer is asking about an Hour appointment (HR): $500, 60 minutes.",
                ServiceId = ServiceCatalog.HourId,
                Active = true
            }
        ];
    }
}
