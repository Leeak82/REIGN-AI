using REIGN.Data.Models;

namespace REIGN.Data.Seed;

public static class ServiceSeed
{
    public static List<Service> GetServices()
    {
        return new()
        {
            new Service
            {
                Name = "Oil Change",
                Price = 89.99m,
                DurationMinutes = 30,
                Active = true
            },
            new Service
            {
                Name = "Brake Service",
                Price = 249.99m,
                DurationMinutes = 60,
                Active = true
            },
            new Service
            {
                Name = "Diagnostic Inspection",
                Price = 129.99m,
                DurationMinutes = 60,
                Active = true
            },
            new Service
            {
                Name = "Vehicle Inspection",
                Price = 79.99m,
                DurationMinutes = 30,
                Active = true
            },
            new Service
            {
                Name = "Quick Visit",
                Price = 200m,
                DurationMinutes = 15,
                Active = true
            },
            new Service
            {
                Name = "Half Hour",
                Price = 300m,
                DurationMinutes = 30,
                Active = true
            },
            new Service
            {
                Name = "Hour",
                Price = 500m,
                DurationMinutes = 60,
                Active = true
            }
        };
    }
}
