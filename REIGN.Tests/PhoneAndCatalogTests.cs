using REIGN.API.Messaging;
using REIGN.API.Options;
using REIGN.Core.Catalog;
using Xunit;

namespace REIGN.Tests;

public class PhoneAndCatalogTests
{
    [Fact]
    public void Catalog_prices_match_reign_qv_hh_hr()
    {
        Assert.Equal(150m, ServiceCatalog.QuickVisitPrice);
        Assert.True(ServiceCatalog.QuickVisitMinutes < 30);
        Assert.Equal(300m, ServiceCatalog.HalfHourPrice);
        Assert.Equal(30, ServiceCatalog.HalfHourMinutes);
        Assert.Equal(500m, ServiceCatalog.HourPrice);
        Assert.Equal(60, ServiceCatalog.HourMinutes);
    }

    [Theory]
    [InlineData("3609261856", "+13609261856")]
    [InlineData("+1 (360) 926-1856", "+13609261856")]
    [InlineData("13609261856", "+13609261856")]
    public void Phone_normalization_uses_e164(string input, string expected)
    {
        Assert.Equal(expected, PhoneNumbers.Normalize(input));
    }

    [Fact]
    public void Business_number_cannot_be_the_owner_personal_number()
    {
        var options = new SmsOptions
        {
            BusinessPhoneNumber = "+15555550199",
            OwnerPhoneNumber = "555-555-0199"
        };

        var result = BusinessNumberGuard.ResolveFromNumber(options, null, null);
        Assert.NotNull(result.Error);
        Assert.Contains("separate", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Dedicated_business_number_is_allowed()
    {
        var options = new SmsOptions
        {
            BusinessPhoneNumber = "+15555550100",
            OwnerPhoneNumber = "+15555550199"
        };

        var result = BusinessNumberGuard.ResolveFromNumber(options, null, null);
        Assert.Null(result.Error);
        Assert.Equal("+15555550100", result.Number);
    }
}
