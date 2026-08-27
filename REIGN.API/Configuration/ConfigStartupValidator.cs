using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using REIGN.Core.Contact;

namespace REIGN.API.Configuration;

public static class ConfigStartupValidator
{
    public static void ValidateAndLog(WebApplication app)
    {
        Validate(
            app.Configuration,
            app.Logger,
            app.Environment.IsProduction(),
            app.Environment.IsDevelopment());
    }

    public static void Validate(
        IConfiguration configuration,
        ILogger logger,
        bool isProduction,
        bool isDevelopment = false)
    {
        logger.LogInformation("REIGN configuration check ({Mode})", isProduction ? "Production" : "Non-production");

        var connection = configuration.GetConnectionString("Reign");
        if (string.IsNullOrWhiteSpace(connection))
        {
            logger.LogWarning(
            "ConnectionStrings:Reign is not set. Local development can use SQLite. Production requires ConnectionStrings__Reign or SUPABASE_PROJECT_REF plus SUPABASE_DB_PASSWORD.");
        }
        else
        {
            logger.LogInformation("Database connection setting is present.");
        }

        if (string.IsNullOrWhiteSpace(configuration["Ai:ApiKey"]))
        {
            logger.LogWarning(
                "Groq API key is missing. Set Ai__ApiKey or GROQ_API_KEY. Miss Reign will use the built-in fallback assistant until a key is provided.");
        }
        else
        {
            logger.LogInformation("Groq API key is present.");
        }

        var smsProvider = configuration["Sms:Provider"] ?? "Simulated";
        logger.LogInformation("SMS provider: {Provider}", smsProvider);

        if (smsProvider.Equals("Twilio", StringComparison.OrdinalIgnoreCase))
        {
            if (Missing(configuration, "Sms:Twilio:AccountSid") || Missing(configuration, "Sms:Twilio:AuthToken"))
            {
                logger.LogError(
                    "Sms:Provider is Twilio, but Sms__Twilio__AccountSid and/or Sms__Twilio__AuthToken are missing. Outbound SMS and POST /api/sms/incoming will not work.");
            }
            else
            {
                logger.LogInformation("Twilio credentials are present.");
                if (!Missing(configuration, "Sms:Twilio:WebhookPublicUrl"))
                {
                    logger.LogInformation("Twilio webhook public URL is configured.");
                }
                else if (!Missing(configuration, "Sms:PublicBaseUrl"))
                {
                    logger.LogInformation("Twilio signatures will use Sms:PublicBaseUrl plus /api/sms/incoming.");
                }
                else
                {
                    logger.LogWarning(
                        "Sms__PublicBaseUrl and TWILIO_WEBHOOK_URL are empty. Set TWILIO_WEBHOOK_URL=https://YOUR_HOST/api/sms/incoming to match the Twilio number A Message Comes In webhook. Sending SMS from the Twilio Console does not call REIGN.");
                }
            }
        }
        else if (smsProvider.Equals("Vonage", StringComparison.OrdinalIgnoreCase))
        {
            if (Missing(configuration, "Sms:Vonage:ApiKey") || Missing(configuration, "Sms:Vonage:ApiSecret"))
            {
                logger.LogError(
                    "Sms:Provider is Vonage, but Sms__Vonage__ApiKey and/or Sms__Vonage__ApiSecret are missing. Outbound SMS will not work.");
            }
            else
            {
                logger.LogInformation("Vonage API credentials are present.");
            }

            if (configuration.GetValue("Sms:Vonage:RequireSignedWebhooks", true) &&
                Missing(configuration, "Sms:Vonage:SignatureSecret"))
            {
                logger.LogError(
                    "Vonage signed webhooks are required, but Sms__Vonage__SignatureSecret is missing. POST /api/sms/webhooks/vonage will reject inbound traffic.");
            }
        }
        else if (smsProvider.Equals("SmsGate", StringComparison.OrdinalIgnoreCase) ||
                 smsProvider.Equals("Android", StringComparison.OrdinalIgnoreCase))
        {
            if (Missing(configuration, "Sms:SmsGate:Username") || Missing(configuration, "Sms:SmsGate:Password"))
            {
                logger.LogError(
                    "Sms:Provider is SmsGate, but Sms__SmsGate__Username and/or Sms__SmsGate__Password are missing. Install SMS Gateway for Android, then set those credentials.");
            }
            else
            {
                logger.LogInformation("SmsGate credentials are present.");
            }

            if (configuration.GetValue("Sms:SmsGate:RequireSignedWebhooks", true) &&
                Missing(configuration, "Sms:SmsGate:SigningKey"))
            {
                logger.LogError(
                    "SmsGate signed webhooks are required, but Sms__SmsGate__SigningKey is missing. POST /api/sms/webhooks/smsgate will reject inbound traffic.");
            }
        }
        else if (smsProvider.Equals("TextNow", StringComparison.OrdinalIgnoreCase))
        {
            logger.LogError("TextNow has no supported application SMS API. Set Sms__Provider to Twilio, Vonage, or SmsGate.");
        }

        if (ReignContact.IsPlaceholder(configuration["Sms:BusinessPhoneNumber"]))
        {
            logger.LogWarning("Sms__BusinessPhoneNumber is not set. Incoming routing and outbound From-number checks need a dedicated REIGN business number.");
        }

        if (Missing(configuration, "Sms:OwnerPhoneNumber"))
        {
            logger.LogWarning("Sms__OwnerPhoneNumber is not set. Owner activity texts may be treated as customer threads.");
        }

        var calendarProvider = configuration["GoogleCalendar:Provider"] ?? "Simulated";
        logger.LogInformation("Calendar provider: {Provider}", calendarProvider);
        logger.LogInformation(
            "Google Calendar OAuth redirect URI: {RedirectUri}",
            string.IsNullOrWhiteSpace(configuration["GoogleCalendar:RedirectUri"])
                ? "(not set)"
                : configuration["GoogleCalendar:RedirectUri"]);

        if (calendarProvider.Equals("Google", StringComparison.OrdinalIgnoreCase))
        {
            if (Missing(configuration, "GoogleCalendar:ClientId") || Missing(configuration, "GoogleCalendar:ClientSecret"))
            {
                logger.LogError(
                    "GoogleCalendar:Provider is Google, but GoogleCalendar__ClientId and/or GoogleCalendar__ClientSecret are missing. OAuth and event create/update/cancel will not work.");
            }
            else
            {
                logger.LogInformation("Google Calendar OAuth client credentials are present. Complete /api/integrations/google/authorize if a refresh token is not stored yet.");
            }

            if (Missing(configuration, "GoogleCalendar:RedirectUri"))
            {
                logger.LogWarning("GoogleCalendar__RedirectUri is not set. The OAuth callback must match the Google Cloud client exactly.");
            }
        }

        var cors = CorsOriginPolicy.Resolve(configuration, isDevelopment);
        if (cors.RejectedWildcard)
        {
            logger.LogError(
                "Cors:AllowedOrigins contained '*'. Wildcard CORS is not allowed. Set CORS_ALLOWED_ORIGINS to explicit https origins.");
        }

        if (cors.Origins.Count == 0)
        {
            logger.LogWarning(
                "No CORS origins are configured. Set CORS_ALLOWED_ORIGINS to the production web origin (for example https://app.example.com).");
        }
        else
        {
            logger.LogInformation("CORS allowed origins: {Count} configured.", cors.Origins.Count);
        }

        var databaseConfigured = !string.IsNullOrWhiteSpace(connection);
        var groqConfigured = !string.IsNullOrWhiteSpace(configuration["Ai:ApiKey"]);
        var smsConfigured = SmsConfigured(configuration, smsProvider);
        var calendarConfigured = CalendarConfigured(configuration, calendarProvider);

        logger.LogInformation(
            "REIGN startup status: database={Database} groq={Groq} sms={Sms} calendar={Calendar}",
            Flag(databaseConfigured),
            Flag(groqConfigured),
            Flag(smsConfigured),
            Flag(calendarConfigured));

        if (!isProduction)
        {
            return;
        }

        var liveSms = SmsProviderSelection.Resolve(
            smsProvider,
            isDevelopment: false,
            configuration["Sms:BusinessPhoneNumber"],
            configuration["Sms:Twilio:FromNumber"]);
        if (SmsProviderSelection.IsSimulated(liveSms))
        {
            throw new InvalidOperationException(
                "Production cannot use Simulated SMS. Set Sms__Provider to SmsGate, Twilio, or Vonage.");
        }

        var liveCalendar = CalendarProviderSelection.Resolve(calendarProvider, isDevelopment: false);
        if (CalendarProviderSelection.IsSimulated(liveCalendar))
        {
            throw new InvalidOperationException(
                "Production cannot use Simulated Calendar. Set GoogleCalendar__Provider=Google, supply OAuth credentials, and complete the consent flow.");
        }

        var allowSimulator = configuration.GetValue("Sms:AllowInternalSimulator", false);
        if (allowSimulator)
        {
            throw new InvalidOperationException(
                "Production cannot enable the internal SMS simulator. Set Sms__AllowInternalSimulator=false.");
        }
    }

    private static string Flag(bool configured) => configured ? "configured" : "not configured";

    private static bool SmsConfigured(IConfiguration configuration, string provider)
    {
        if (provider.Equals("Simulated", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (provider.Equals("Twilio", StringComparison.OrdinalIgnoreCase))
        {
            return !Missing(configuration, "Sms:Twilio:AccountSid")
                && !Missing(configuration, "Sms:Twilio:AuthToken");
        }

        if (provider.Equals("Vonage", StringComparison.OrdinalIgnoreCase))
        {
            return !Missing(configuration, "Sms:Vonage:ApiKey")
                && !Missing(configuration, "Sms:Vonage:ApiSecret");
        }

        if (provider.Equals("SmsGate", StringComparison.OrdinalIgnoreCase) ||
            provider.Equals("Android", StringComparison.OrdinalIgnoreCase))
        {
            return !Missing(configuration, "Sms:SmsGate:Username")
                && !Missing(configuration, "Sms:SmsGate:Password");
        }

        return false;
    }

    private static bool CalendarConfigured(IConfiguration configuration, string provider)
    {
        if (provider.Equals("Simulated", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return !Missing(configuration, "GoogleCalendar:ClientId")
            && !Missing(configuration, "GoogleCalendar:ClientSecret");
    }

    private static bool Missing(IConfiguration configuration, string key) =>
        string.IsNullOrWhiteSpace(configuration[key]);
}
