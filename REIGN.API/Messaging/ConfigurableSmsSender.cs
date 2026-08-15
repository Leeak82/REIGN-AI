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
        TextNowUnsupportedSmsSender textNow,
        IHostEnvironment environment)
    {
        var provider = SmsProviderSelection.Resolve(options.Value.Provider, environment.IsDevelopment());
        _inner = provider.Trim().ToLowerInvariant() switch
        {
            "twilio" => twilio,
            "vonage" => vonage,
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
