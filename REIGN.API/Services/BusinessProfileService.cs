using Microsoft.EntityFrameworkCore;
using REIGN.Data;
using REIGN.Data.Models;
using REIGN.Data.Seed;

namespace REIGN.API.Services;

public class BusinessProfile
{
    public Guid Id { get; set; }

    public string Name { get; set; } = "REIGN";

    public string AssistantName { get; set; } = "Miss Reign";

    public string Offering { get; set; } = "Private appointment scheduling for Quick Visit, Half Hour, and Hour sessions.";

    public string Hours { get; set; } = "By appointment, typically 9am–5pm. Same-day visits need at least one hour notice.";

    public string Tone { get; set; } = "Warm, concise, professional. SMS replies stay under 320 characters.";

    public string TimeZone { get; set; } = "America/New_York";

    public string Greeting { get; set; } = "";

    public string Personality { get; set; } = "";

    public string Instructions { get; set; } = "";

    public string ToPrompt() =>
        $"{Name}. {Offering} Hours: {Hours} Tone: {Tone}";
}

public interface IBusinessProfileAccessor
{
    Task<BusinessProfile> GetActiveAsync(CancellationToken cancellationToken = default);
}

public class BusinessProfileService : IBusinessProfileAccessor
{
    private readonly ReignDbContext _db;

    public BusinessProfileService(ReignDbContext db)
    {
        _db = db;
    }

    public async Task<BusinessProfile> GetActiveAsync(CancellationToken cancellationToken = default)
    {
        var business = await _db.Businesses
            .AsNoTracking()
            .Include(x => x.AIProfiles)
            .Where(x => x.Active)
            .OrderBy(x => x.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken)
            ?? await _db.Businesses
                .AsNoTracking()
                .Include(x => x.AIProfiles)
                .OrderBy(x => x.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);

        var seed = BusinessSeed.GetBusiness();
        var profileSeed = BusinessSeed.GetAIProfile();
        var ai = business?.AIProfiles.FirstOrDefault(x => x.Active)
            ?? business?.AIProfiles.FirstOrDefault();

        return new BusinessProfile
        {
            Id = business?.Id ?? seed.Id,
            Name = NonEmpty(business?.Name, seed.Name),
            AssistantName = NonEmpty(ai?.AIName, profileSeed.AIName),
            Offering = NonEmpty(ai?.BusinessDescription, profileSeed.BusinessDescription),
            Hours = NonEmpty(business?.Hours, seed.Hours),
            Tone = NonEmpty(ai?.Personality, business?.Tone, seed.Tone),
            TimeZone = NonEmpty(business?.TimeZone, seed.TimeZone),
            Greeting = NonEmpty(ai?.Greeting, business?.Greeting, seed.Greeting),
            Personality = NonEmpty(ai?.Personality, business?.Personality, seed.Personality),
            Instructions = NonEmpty(business?.Instructions, seed.Instructions)
        };
    }

    private static string NonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return "";
    }
}
