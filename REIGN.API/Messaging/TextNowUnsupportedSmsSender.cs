namespace REIGN.API.Messaging;

/// <summary>
/// TextNow is a consumer phone product. It does not publish a supported application SMS API,
/// inbound webhooks, or signing secrets. Unofficial cookie scrapers are not used here.
/// </summary>
public class TextNowUnsupportedSmsSender : ISmsSender
{
    public const string Reason =
        "TextNow does not provide a legitimate supported SMS API for applications. " +
        "Use Simulated for local development, Twilio/Vonage with a dedicated business number, or SmsGate with an Android SIM.";

    public string ProviderName => "TextNow";

    public bool IsConfigured => false;

    public bool IsSimulated => false;

    public Task<SmsSendResult> SendAsync(SmsSendRequest request, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(SmsSendResult.Fail(ProviderName, Reason));
    }
}
