using REIGN.Data.Models;

namespace REIGN.Data.Seed;

public static class ServiceRecommendationSeed
{

    public static List<ServiceRecommendation> Get()
    {

        return new()
        {

            new ServiceRecommendation
            {
                Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                Trigger = "quick",
                Recommendation = "Customer likely wants a QV (Quick Visit).",
                ServiceId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Active = true
            },


            new ServiceRecommendation
            {
                Id = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
                Trigger = "half",
                Recommendation = "Customer likely wants an HH (Half Hour) visit.",
                ServiceId = Guid.Parse("22222222-2222-2222-2222-222222222222"),
                Active = true
            },


            new ServiceRecommendation
            {
                Id = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"),
                Trigger = "hour",
                Recommendation = "Customer likely wants an HR (One Hour) visit.",
                ServiceId = Guid.Parse("33333333-3333-3333-3333-333333333333"),
                Active = true
            }

        };

    }

}
