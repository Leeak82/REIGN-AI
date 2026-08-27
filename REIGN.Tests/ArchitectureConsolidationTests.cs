using Microsoft.EntityFrameworkCore;
using REIGN.API.Services;
using REIGN.Core.Catalog;
using REIGN.Core.Contact;
using REIGN.Data.Seed;
using Xunit;

namespace REIGN.Tests;

public class ArchitectureConsolidationTests
{
    [Fact]
    public async Task Business_and_ai_profile_come_from_ef_not_options()
    {
        await using var harness = await IncomingSmsProcessorTests.Harness.CreateAsync();
        var accessor = new BusinessProfileService(harness.Db);
        var profile = await accessor.GetActiveAsync();

        Assert.Equal("REIGN", profile.Name);
        Assert.Equal("Miss Reign", profile.AssistantName);
        Assert.Equal(ReignContact.ProviderFullName, BusinessSeed.GetBusiness().OwnerName);
        Assert.Contains("Jessica", BusinessSeed.GetBusiness().Greeting, StringComparison.Ordinal);
        Assert.Equal(ReignContact.ProviderCalendar, BusinessSeed.GetBusiness().Email);
        Assert.True(await harness.Db.Businesses.AnyAsync(x => x.OwnerName == ReignContact.ProviderFullName));
        Assert.Equal("America/Los_Angeles", profile.TimeZone);
        Assert.Contains("Quick Visit", profile.Offering);
        Assert.False(string.IsNullOrWhiteSpace(profile.Hours));
        Assert.Equal(BusinessSeed.BusinessId, profile.Id);
        Assert.Equal(ReignContact.BusinessPhoneE164, BusinessSeed.GetBusiness().Phone);
        Assert.True(await harness.Db.Businesses.AnyAsync(x => x.Phone == ReignContact.BusinessPhoneE164));
        Assert.True(await harness.Db.Businesses.AnyAsync());
        Assert.True(await harness.Db.BusinessAIProfiles.AnyAsync(x => x.AIName == "Miss Reign"));
    }

    [Fact]
    public async Task Catalog_bootstrap_uses_canonical_qv_hh_hr_ids()
    {
        await using var harness = await IncomingSmsProcessorTests.Harness.CreateAsync();
        var catalog = new CatalogIntelligence(harness.Db);
        var payload = await catalog.GetCatalogAsync();
        var json = System.Text.Json.JsonSerializer.Serialize(payload);

        Assert.Contains(ServiceCatalog.QuickVisitName, json);
        Assert.Contains(ServiceCatalog.HalfHourName, json);
        Assert.Contains(ServiceCatalog.HourName, json);
        Assert.DoesNotContain("oil", json, StringComparison.OrdinalIgnoreCase);
        Assert.True(await harness.Db.Services.AnyAsync(x => x.Id == ServiceCatalog.QuickVisitId && x.BusinessId != null));
    }
}
