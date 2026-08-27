using REIGN.Core.Contact;

namespace REIGN.API.Options;

public class SmsOptions
{
    public const string SectionName = "Sms";

    /// <summary>
    /// Simulated (Development/tests only), Twilio, Vonage, SmsGate, or TextNow.
    /// Production uses SmsGate for the Straight Talk SIM while Twilio A2P is pending.
    /// TextNow has no supported application SMS API and will not send or receive.
    /// SmsGate is the open-source Android SMS gateway (a real SIM on a phone).
    /// </summary>
    public string Provider { get; set; } = "Simulated";

    /// <summary>
    /// Dedicated REIGN business number. Must not be the owner's personal cell.
    /// </summary>
    public string BusinessPhoneNumber { get; set; } = ReignContact.BusinessPhoneE164;

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
    /// Shared secret for the internal JSON /api/sms/incoming simulator. Empty allows the simulator in Development only. Twilio form POSTs to the same path do not use this key.
    /// </summary>
    public string InternalApiKey { get; set; } = "";

    public bool AllowInternalSimulator { get; set; } = true;

    public TwilioSmsOptions Twilio { get; set; } = new();

    public VonageSmsOptions Vonage { get; set; } = new();

    public SmsGateOptions SmsGate { get; set; } = new();
}

public class TwilioSmsOptions
{
    public string AccountSid { get; set; } = "";

    public string AuthToken { get; set; } = "";

    public string FromNumber { get; set; } = "";

    /// <summary>
    /// Exact public URL Twilio is configured to POST to, if it differs from the app's local request URL.
    /// Example: https://reign-ai-2.onrender.com/api/sms/incoming
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

/// <summary>
/// Open-source Android SMS gateway (https://sms-gate.app / capcom6/android-sms-gateway).
/// Uses a real SIM on a phone. Not a carrier CPaaS.
/// </summary>
public class SmsGateOptions
{
    public string Username { get; set; } = "";

    public string Password { get; set; } = "";

    /// <summary>
    /// Cloud default is https://api.sms-gate.app/3rdparty/v1. Local mode is the phone's LAN URL.
    /// </summary>
    public string BaseUrl { get; set; } = "https://api.sms-gate.app/3rdparty/v1";

    /// <summary>
    /// HMAC-SHA256 signing key from the app Settings → Webhooks → Signing Key.
    /// This is a secret, not the webhook URL.
    /// </summary>
    public string SigningKey { get; set; } = "";

    /// <summary>
    /// Cloud device id from the app Home tab. Pins outbound SMS to that phone.
    /// </summary>
    public string DeviceId { get; set; } = "";

    /// <summary>
    /// 1-based SIM slot. Use 1 for the Straight Talk *1244 line on the dual-SIM Motorola.
    /// 0 means let the app choose/rotate.
    /// </summary>
    public int SimNumber { get; set; }

    public string FromNumber { get; set; } = ReignContact.BusinessPhoneE164;

    /// <summary>
    /// Other SIMs in the gateway phone. Comma-separated E.164. Inbound from these
    /// numbers is the phone talking to itself, not a customer.
    /// </summary>
    public string IgnoreFromNumbers { get; set; } = "";

    public bool RequireSignedWebhooks { get; set; } = true;
}
