using REIGN.API.Messaging;
using REIGN.API.Options;
using REIGN.Core.Catalog;
using REIGN.Core.Contact;
using REIGN.Data.Seed;
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
    public void Short_codes_and_the_business_sim_are_not_replyable_customers()
    {
        Assert.True(PhoneNumbers.IsShortCode("611611"));
        Assert.False(PhoneNumbers.IsReplyableCustomerNumber("611611"));
        Assert.False(PhoneNumbers.IsReplyableCustomerNumber(ReignContact.BusinessPhoneE164));
        Assert.False(PhoneNumbers.IsReplyableCustomerNumber("+15555550999"));
        Assert.True(PhoneNumbers.IsReplyableCustomerNumber("+19072132242"));
        Assert.True(PhoneNumbers.IsReplyableCustomerNumber("3609261856"));
    }

    [Fact]
    public void Inbound_endpoints_prefer_the_customer_handset()
    {
        var own = PhoneNumbers.GatewayOwnNumbers(
            ReignContact.BusinessPhoneE164,
            ReignContact.BusinessPhoneE164,
            "+19072132242");

        var swapped = PhoneNumbers.ResolveInboundEndpoints(
            ReignContact.BusinessPhoneE164,
            "+13609261856",
            null,
            own);
        Assert.Equal("+13609261856", swapped.From);
        Assert.Equal(ReignContact.BusinessPhoneE164, swapped.To);
        Assert.True(swapped.Swapped);

        var official = PhoneNumbers.ResolveInboundEndpoints(
            "+13609261856",
            ReignContact.BusinessPhoneE164,
            null,
            own);
        Assert.Equal("+13609261856", official.From);
        Assert.Equal(ReignContact.BusinessPhoneE164, official.To);
        Assert.False(official.Swapped);

        var phoneNumberOnly = PhoneNumbers.ResolveInboundEndpoints(
            ReignContact.BusinessPhoneE164,
            ReignContact.BusinessPhoneE164,
            "+13609261856",
            own);
        Assert.Equal("+13609261856", phoneNumberOnly.From);
        Assert.True(phoneNumberOnly.Swapped);

        var skipCalls = PhoneNumbers.GatewayOwnNumbers(
            ReignContact.BusinessPhoneE164,
            ReignContact.BusinessPhoneE164,
            "+19072132242",
            "+18136380375");
        var skipInbound = PhoneNumbers.ResolveInboundEndpoints(
            "+18136380375",
            "+12538319100",
            "+12538319100",
            skipCalls);
        Assert.Equal("+12538319100", skipInbound.From);
        Assert.Equal("+18136380375", skipInbound.To);
        Assert.True(skipInbound.Swapped);

        var bothOwn = PhoneNumbers.ResolveInboundEndpoints(
            ReignContact.BusinessPhoneE164,
            "+19072132242",
            null,
            own);
        Assert.Equal(ReignContact.BusinessPhoneE164, bothOwn.From);
        Assert.Equal("+19072132242", bothOwn.To);
        Assert.False(bothOwn.Swapped);
        Assert.True(PhoneNumbers.IsOwnDeviceNumber("+19072132242", own));
    }

    [Fact]
    public void Public_name_is_miss_reign()
    {
        Assert.Equal("Miss Reign", ReignContact.PublicName);
        Assert.Equal("hello@reign.ai", ReignContact.PublicEmail);
        Assert.DoesNotContain("Jessica", BusinessSeed.GetBusiness().Greeting, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Jessica", BusinessSeed.GetAIProfile().Greeting, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Jessica", BusinessSeed.GetAIProfile().BusinessDescription, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Jessica", BusinessSeed.GetBusiness().OwnerName, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Jessica", BusinessSeed.GetBusiness().Email, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("j.collins2491@gmail.com", ReignContact.ProviderCalendar);
        Assert.Equal("j.collins2491@gmail.com", ReignContact.CalendarAccountForDisplay("primary"));
        Assert.Equal("j.collins2491@gmail.com", ReignContact.CalendarAccountForDisplay(null));
        Assert.Equal("j.collins2491@gmail.com", ReignContact.CalendarAccountForDisplay(""));
        Assert.Equal("other@example.com", ReignContact.CalendarAccountForDisplay("other@example.com"));
    }
}
