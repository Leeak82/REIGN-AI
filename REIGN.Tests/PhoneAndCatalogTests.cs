using REIGN.API.Messaging;
using REIGN.API.Options;
using REIGN.Core.Catalog;
using REIGN.Core.Contact;
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
            BusinessPhoneNumber = ReignContact.BusinessPhoneE164,
            OwnerPhoneNumber = "+15555550199"
        };

        var result = BusinessNumberGuard.ResolveFromNumber(options, null, null);
        Assert.Null(result.Error);
        Assert.Equal(ReignContact.BusinessPhoneE164, result.Number);
    }

    [Theory]
    [InlineData("9073001244", "+19073001244")]
    [InlineData("+1 (907) 300-1244", "+19073001244")]
    [InlineData("19073001244", "+19073001244")]
    public void Straight_talk_business_number_normalizes_to_e164(string input, string expected)
    {
        Assert.Equal(expected, PhoneNumbers.Normalize(input));
        Assert.Equal(ReignContact.BusinessPhoneDisplay, PhoneNumbers.FormatDisplay(input));
    }

    [Fact]
    public void Fictional_555_numbers_are_placeholders()
    {
        Assert.True(ReignContact.IsPlaceholder("+15555550100"));
        Assert.True(ReignContact.IsPlaceholder("555-555-0199"));
        Assert.True(ReignContact.IsPlaceholder(""));
        Assert.True(ReignContact.IsPlaceholder("+13605550100"));
        Assert.False(ReignContact.IsPlaceholder("+12065551234"));
        Assert.False(ReignContact.IsPlaceholder(ReignContact.BusinessPhoneE164));
        Assert.False(ReignContact.IsPlaceholder("9073001244"));
    }

    [Fact]
    public void Jessica_is_the_provider_and_calendar_owner()
    {
        Assert.Equal("Jessica", ReignContact.ProviderFirstName);
        Assert.Equal("Jessica Collins", ReignContact.ProviderFullName);
        Assert.Equal("j.collins2491@gmail.com", ReignContact.ProviderCalendar);
        Assert.Equal("j.collins2491@gmail.com", ReignContact.CalendarAccountForDisplay("primary"));
        Assert.Equal("j.collins2491@gmail.com", ReignContact.CalendarAccountForDisplay(null));
        Assert.Equal("j.collins2491@gmail.com", ReignContact.CalendarAccountForDisplay(""));
        Assert.Equal("other@example.com", ReignContact.CalendarAccountForDisplay("other@example.com"));
    }
}
