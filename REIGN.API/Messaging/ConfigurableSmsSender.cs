using REIGN.API.Configuration;
using Microsoft.Extensions.Options;
using REIGN.API.Options;

namespace REIGN.API.Messaging;

public class ConfigurableSmsSender : ISmsSender
{
    private readonly ISmsSender _inner;

    public ConfigurableSmsSender(
        IOptions<SmsOptions> options,
        SimulatedSmsSender simulated,
        TwilioSmsSender twilio,
        VonageSmsSender vonage,
        SmsGateSmsSender smsGate,
        SkipCallsSmsSender skipCalls,
        TextNowUnsupportedSmsSender textNow,
        IHostEnvironment environment)
    {
        var sms = options.Value;
        var provider = SmsProviderSelection.Resolve(
            sms.Provider,
            environment.IsDevelopment(),
            sms.BusinessPhoneNumber,
            sms.Twilio.FromNumber);
        if (!environment.IsDevelopment() && SmsProviderSelection.IsSimulated(provider))
        {
            throw new InvalidOperationException(
                "Production cannot use Simulated SMS. Set Sms__Provider to SmsGate, SkipCalls, Twilio, or Vonage.");
        }

        _inner = provider.Trim().ToLowerInvariant() switch
        {
            "twilio" => twilio,
            "vonage" => vonage,
            "smsgate" or "android" or "android-sms-gateway" => smsGate,
            "skipcalls" or "skip-calls" or "cail" => skipCalls,
            "textnow" => textNow,
            _ => environment.IsDevelopment() ? simulated : twilio
        };
    }

    public string ProviderName => _inner.ProviderName;

    public bool IsConfigured => _inner.IsConfigured;

    public bool IsSimulated => _inner.IsSimulated;

    public Task<SmsSendResult> SendAsync(SmsSendRequest request, CancellationToken cancellationToken = default) =>
        _inner.SendAsync(request, cancellationToken);
}
