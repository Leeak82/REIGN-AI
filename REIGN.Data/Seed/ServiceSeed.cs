using REIGN.Data.Models;

namespace REIGN.Data.Seed;

public static class ServiceSeed
{
    private static readonly Guid BusinessId =
        Guid.Parse("99999999-9999-9999-9999-999999999999");

    public static List<Service> GetServices()
    {
        return new()
        {
            new Service
            {
                Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                BusinessId = BusinessId,
                Name = "QV",
                Description = "Quick Visit — less than 30 minutes",
                Price = 150m,
                DurationMinutes = 29,
                Active = true
            },

            new Service
            {
                Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
                BusinessId = BusinessId,
                Name = "HH",
                Description = "Half Hour",
                Price = 300m,
                DurationMinutes = 30,
                Active = true
            },

            new Service
            {
                Id = Guid.Parse("33333333-3333-3333-3333-333333333333"),
                BusinessId = BusinessId,
                Name = "HR",
                Description = "One Hour",
                Price = 500m,
                DurationMinutes = 60,
                Active = true
            }
        };
    }
}
