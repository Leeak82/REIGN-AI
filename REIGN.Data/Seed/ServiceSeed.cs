using REIGN.Core.Catalog;
using REIGN.Data.Models;

namespace REIGN.Data.Seed;

public static class ServiceSeed
{
    public static List<Service> GetServices()
    {
        return
        [
            new Service
            {
                Id = ServiceCatalog.QuickVisitId,
                Name = ServiceCatalog.QuickVisitName,
                Price = ServiceCatalog.QuickVisitPrice,
                DurationMinutes = ServiceCatalog.QuickVisitMinutes,
                Active = true
            },
            new Service
            {
                Id = ServiceCatalog.HalfHourId,
                Name = ServiceCatalog.HalfHourName,
                Price = ServiceCatalog.HalfHourPrice,
                DurationMinutes = ServiceCatalog.HalfHourMinutes,
                Active = true
            },
            new Service
            {
                Id = ServiceCatalog.HourId,
                Name = ServiceCatalog.HourName,
                Price = ServiceCatalog.HourPrice,
                DurationMinutes = ServiceCatalog.HourMinutes,
                Active = true
            }
        ];
    }
}
