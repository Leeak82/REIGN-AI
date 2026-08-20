using Microsoft.Extensions.Configuration;

namespace REIGN.API.Configuration;

/// <summary>
/// Copies well-known environment variable names into the nested configuration keys REIGN already binds.
/// Existing non-empty configuration values (including Ai__ApiKey-style env keys) win.
/// </summary>
public static class ConfigEnvironmentAliases
{
    public static void Apply(ConfigurationManager configuration)
    {
        var extras = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        TryAlias(configuration, extras, "Ai:ApiKey", "GROQ_API_KEY", "GROQ_KEY");
        TryAlias(configuration, extras, "GoogleCalendar:ClientId", "GOOGLE_CLIENT_ID", "GOOGLE_CALENDAR_CLIENT_ID");
        TryAlias(configuration, extras, "GoogleCalendar:ClientSecret", "GOOGLE_CLIENT_SECRET", "GOOGLE_CALENDAR_CLIENT_SECRET");
        // appsettings.json ships non-empty RedirectUri/CalendarId/TimeZone defaults. Those
        // must not block GOOGLE_* aliases. Nested GoogleCalendar__* env keys still win here;
        // GoogleRedirectUri.Apply then last-wins so a Development container cannot keep the
        // Kestrel https://localhost:5001 callback.
        TryAlias(
            configuration,
            extras,
            "GoogleCalendar:RedirectUri",
            allowOverride: true,
            "GOOGLE_REDIRECT_URI",
            "GOOGLE_CALENDAR_REDIRECT_URI");
        TryAlias(configuration, extras, "GoogleCalendar:CalendarId", allowOverride: true, "GOOGLE_CALENDAR_ID");
        TryAlias(configuration, extras, "GoogleCalendar:TimeZone", allowOverride: true, "GOOGLE_CALENDAR_TIMEZONE");
        TryAlias(configuration, extras, "Sms:Twilio:AccountSid", "TWILIO_ACCOUNT_SID");
        TryAlias(configuration, extras, "Sms:Twilio:AuthToken", "TWILIO_AUTH_TOKEN");
        TryAlias(configuration, extras, "Sms:Twilio:FromNumber", "TWILIO_FROM_NUMBER", "TWILIO_PHONE_NUMBER");
        TryAlias(configuration, extras, "Sms:Twilio:WebhookPublicUrl", "TWILIO_WEBHOOK_URL");
        TryAlias(configuration, extras, "Sms:SmsGate:Username", "SMSGATE_USERNAME");
        TryAlias(configuration, extras, "Sms:SmsGate:Password", "SMSGATE_PASSWORD");
        TryAlias(configuration, extras, "Sms:SmsGate:BaseUrl", "SMSGATE_BASE_URL");
        TryAlias(configuration, extras, "Sms:SmsGate:SigningKey", "SMSGATE_SIGNING_KEY");
        TryAlias(configuration, extras, "Sms:SmsGate:FromNumber", "SMSGATE_FROM_NUMBER");
        TryAlias(configuration, extras, "Sms:Vonage:ApiKey", "VONAGE_API_KEY");
        TryAlias(configuration, extras, "Sms:Vonage:ApiSecret", "VONAGE_API_SECRET");
        TryAlias(configuration, extras, "Sms:Vonage:SignatureSecret", "VONAGE_SIGNATURE_SECRET");
        TryAlias(configuration, extras, "Sms:Vonage:FromNumber", "VONAGE_FROM_NUMBER");
        TryAlias(configuration, extras, "Sms:BusinessPhoneNumber", "REIGN_BUSINESS_PHONE");
        TryAlias(configuration, extras, "Sms:OwnerPhoneNumber", "REIGN_OWNER_PHONE");
        TryAlias(configuration, extras, "Sms:InternalApiKey", "REIGN_INTERNAL_API_KEY");
        TryAlias(configuration, extras, "Sms:PublicBaseUrl", "REIGN_PUBLIC_BASE_URL", "RENDER_EXTERNAL_URL");
        if (string.IsNullOrWhiteSpace(configuration["Sms:PublicBaseUrl"]) &&
            !extras.ContainsKey("Sms:PublicBaseUrl"))
        {
            var renderHost = Environment.GetEnvironmentVariable("RENDER_EXTERNAL_HOSTNAME");
            if (!string.IsNullOrWhiteSpace(renderHost))
            {
                extras["Sms:PublicBaseUrl"] = "https://" + renderHost.Trim().TrimEnd('/');
            }
        }
        TryAlias(configuration, extras, "ReignApi:BaseUrl", "REIGN_API_BASE_URL");
        TryAlias(configuration, extras, "Cors:AllowedOrigins", "CORS_ALLOWED_ORIGINS");
        TryAlias(
            configuration,
            extras,
            "ConnectionStrings:Reign",
            "ConnectionStrings__Reign",
            "DATABASE_URL",
            "REIGN_CONNECTION_STRING",
            "SUPABASE_DB_URL");

        var smsProvider = Environment.GetEnvironmentVariable("SMS_PROVIDER");
        if (!string.IsNullOrWhiteSpace(smsProvider))
        {
            extras["Sms:Provider"] = smsProvider.Trim();
        }

        if (extras.Count > 0)
        {
            configuration.AddInMemoryCollection(extras);
        }
    }

    public static void ApplyRuntimeSmsDefaults(ConfigurationManager configuration, IHostEnvironment environment)
    {
        var extras = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        extras["Sms:Provider"] = SmsProviderSelection.Resolve(
            configuration["Sms:Provider"],
            environment.IsDevelopment());

        if (!environment.IsDevelopment())
        {
            extras["Sms:AllowInternalSimulator"] = "false";
        }

        configuration.AddInMemoryCollection(extras);
    }

    public static void TryAlias(
        IConfiguration configuration,
        IDictionary<string, string?> extras,
        string configurationKey,
        params string[] environmentNames) =>
        TryAlias(configuration, extras, configurationKey, allowOverride: false, environmentNames);

    public static void TryAlias(
        IConfiguration configuration,
        IDictionary<string, string?> extras,
        string configurationKey,
        bool allowOverride,
        params string[] environmentNames)
    {
        var nestedEnvName = configurationKey.Replace(":", "__", StringComparison.Ordinal);
        if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(nestedEnvName)))
        {
            return;
        }

        if (!allowOverride && !string.IsNullOrWhiteSpace(configuration[configurationKey]))
        {
            return;
        }

        foreach (var name in environmentNames)
        {
            var value = Environment.GetEnvironmentVariable(name);
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            extras[configurationKey] = value;
            return;
        }
    }
}
