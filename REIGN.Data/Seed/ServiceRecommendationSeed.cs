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
                Trigger = "oil",
                Recommendation = "Customer likely needs routine oil maintenance.",
                ServiceId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Active = true
            },


            new ServiceRecommendation
            {
                Id = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
                Trigger = "brake",
                Recommendation = "Customer may need brake inspection or brake service.",
                ServiceId = Guid.Parse("22222222-2222-2222-2222-222222222222"),
                Active = true
            },


            new ServiceRecommendation
            {
                Id = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"),
                Trigger = "check engine",
                Recommendation = "Customer requires diagnostic inspection.",
                ServiceId = Guid.Parse("33333333-3333-3333-3333-333333333333"),
                Active = true
            },


            new ServiceRecommendation
            {
                Id = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"),
                Trigger = "inspection",
                Recommendation = "Recommend a complete vehicle inspection.",
                ServiceId = Guid.Parse("44444444-4444-4444-4444-444444444444"),
                Active = true
            }

        };

    }

}
