using REIGN.API.Options;

namespace REIGN.API.Messaging;

public static class BusinessNumberGuard
{
    public readonly record struct FromNumberResult(string Number, string? Error);

    public static FromNumberResult ResolveFromNumber(SmsOptions options, string? providerFrom, string? requestedFrom)
    {
        var owner = PhoneNumbers.Normalize(options.OwnerPhoneNumber);
        var business = PhoneNumbers.Normalize(
            FirstNonEmpty(providerFrom, requestedFrom, options.BusinessPhoneNumber));

        if (string.IsNullOrWhiteSpace(business))
        {
            return new FromNumberResult("", "A dedicated REIGN business number is not configured.");
        }

        if (!string.IsNullOrWhiteSpace(owner) && PhoneNumbers.AreSame(business, owner))
        {
            return new FromNumberResult("",
                "The REIGN business number must be separate from the owner's personal number. " +
                "Provision a dedicated Twilio or Vonage number instead of toggling a personal cell.");
        }

        return new FromNumberResult(business, null);
    }

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)) ?? "";
}
