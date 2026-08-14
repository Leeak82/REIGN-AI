namespace REIGN.API.Options;

public class SmsOptions
{
    public const string SectionName = "Sms";

    /// <summary>
    /// Simulated (Development/tests only), Twilio, Vonage, or TextNow.
    /// Production defaults to Twilio unless SMS_PROVIDER is Vonage.
    /// TextNow has no supported application SMS API and will not send or receive.
    /// </summary>
    public string Provider { get; set; } = "Simulated";

    /// <summary>
    /// Dedicated REIGN business number. Must not be the owner's personal cell.
    /// </summary>
    public string BusinessPhoneNumber { get; set; } = "+15555550100";

    /// <summary>
    /// Owner's personal number. Never used as the customer-facing From number.
    /// </summary>
    public string OwnerPhoneNumber { get; set; } = "";

    /// <summary>
    /// Public origin used to reconstruct webhook URLs behind TLS-terminating proxies.
    /// Example: https://reign-ai.onrender.com
    /// </summary>
    public string PublicBaseUrl { get; set; } = "";

    /// <summary>
    /// Shared secret for the internal /api/sms/incoming simulator. Empty allows the simulator in Development only.
    /// </summary>
    public string InternalApiKey { get; set; } = "";

    public bool AllowInternalSimulator { get; set; } = true;

    public TwilioSmsOptions Twilio { get; set; } = new();

    public VonageSmsOptions Vonage { get; set; } = new();
}

public class TwilioSmsOptions
{
    public string AccountSid { get; set; } = "";

    public string AuthToken { get; set; } = "";

    public string FromNumber { get; set; } = "";

    /// <summary>
    /// Exact public URL Twilio is configured to POST to, if it differs from the app's local request URL.
    /// </summary>
    public string WebhookPublicUrl { get; set; } = "";
}

public class VonageSmsOptions
{
    public string ApiKey { get; set; } = "";

    public string ApiSecret { get; set; } = "";

    public string SignatureSecret { get; set; } = "";

    public string ApplicationId { get; set; } = "";

    public string FromNumber { get; set; } = "";

    public string WebhookPublicUrl { get; set; } = "";

    /// <summary>
    /// When true (default), Messages API JWTs are required on /api/sms/webhooks/vonage.
    /// Classic SMS inbound uses a shared InternalApiKey or signature secret query/form `sig`.
    /// </summary>
    public bool RequireSignedWebhooks { get; set; } = true;
}
