using REIGN.Data.Models;

namespace REIGN.Data.Seed;

public static class BusinessSeed
{
    public static readonly Guid BusinessId =
        Guid.Parse("99999999-9999-9999-9999-999999999999");

    public static readonly Guid AIProfileId =
        Guid.Parse("88888888-8888-4888-8888-888888888888");

    public static Business GetBusiness()
    {
        return new Business
        {
            Id = BusinessId,
            Name = "REIGN",
            OwnerName = "Miss Reign",
            Phone = "+15555550100",
            Email = "hello@reign.ai",
            Address = "100 Main Street",
            Industry = "Appointment Services",
            Active = true,
            Greeting = "Welcome to REIGN. How can we help you schedule a visit today?",
            Tone = "Warm, concise, professional. SMS replies stay under 320 characters.",
            Personality = "Expert appointment coordinator",
            Instructions = "Help customers understand QV, HH, and HR visits, pricing, and scheduling. Never invent prices or services.",
            Hours = "By appointment, typically 9am–5pm. Same-day visits need at least one hour notice.",
            TimeZone = "America/Los_Angeles",
            CreatedAt = new DateTime(2026, 8, 11, 0, 0, 0, DateTimeKind.Utc)
        };
    }

    public static BusinessAIProfile GetAIProfile()
    {
        return new BusinessAIProfile
        {
            Id = AIProfileId,
            BusinessId = BusinessId,
            AIName = "Miss Reign",
            Personality = "Warm, concise, professional. SMS replies stay under 320 characters.",
            Greeting = "Welcome to REIGN. How can we help you schedule a visit today?",
            BusinessDescription = "Private appointment scheduling for Quick Visit, Half Hour, and Hour sessions.",
            Active = true
        };
    }
}
