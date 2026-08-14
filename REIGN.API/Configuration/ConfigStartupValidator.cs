namespace REIGN.API.Configuration;

public static class ConfigStartupValidator
{
    public static void ValidateAndLog(WebApplication app)
    {
        Validate(app.Configuration, app.Logger, app.Environment.IsProduction());
    }

    public static void Validate(IConfiguration configuration, ILogger logger, bool isProduction)
    {
        logger.LogInformation("REIGN configuration check ({Mode})", isProduction ? "Production" : "Non-production");

        var connection = configuration.GetConnectionString("Reign");
        if (string.IsNullOrWhiteSpace(connection))
        {
            logger.LogWarning(
                "ConnectionStrings:Reign is not set. REIGN will use a local SQLite file under the API content root. Set ConnectionStrings__Reign (or REIGN_CONNECTION_STRING) before live deployment.");
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
                    "Sms:Provider is Twilio, but Sms__Twilio__AccountSid and/or Sms__Twilio__AuthToken are missing. Outbound SMS and POST /api/sms/webhooks/twilio will not work.");
            }
            else
            {
                logger.LogInformation("Twilio credentials are present.");
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
        else if (smsProvider.Equals("TextNow", StringComparison.OrdinalIgnoreCase))
        {
            logger.LogError("TextNow has no supported application SMS API. Set Sms__Provider to Simulated, Twilio, or Vonage.");
        }

        if (Missing(configuration, "Sms:BusinessPhoneNumber"))
        {
            logger.LogWarning("Sms__BusinessPhoneNumber is not set. Incoming routing and outbound From-number checks need a dedicated REIGN business number.");
        }

        if (Missing(configuration, "Sms:OwnerPhoneNumber"))
        {
            logger.LogWarning("Sms__OwnerPhoneNumber is not set. Owner activity texts may be treated as customer threads.");
        }

        var calendarProvider = configuration["GoogleCalendar:Provider"] ?? "Simulated";
        logger.LogInformation("Calendar provider: {Provider}", calendarProvider);

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

        if (!isProduction)
        {
            return;
        }

        if (smsProvider.Equals("Simulated", StringComparison.OrdinalIgnoreCase))
        {
            logger.LogWarning("Production is using Simulated SMS. Set Sms__Provider to Twilio or Vonage before live customer traffic.");
        }

        if (calendarProvider.Equals("Simulated", StringComparison.OrdinalIgnoreCase))
        {
            logger.LogWarning("Production is using Simulated Calendar. Set GoogleCalendar__Provider=Google, supply OAuth credentials, and complete the consent flow.");
        }

        var allowSimulator = configuration.GetValue("Sms:AllowInternalSimulator", false);
        if (allowSimulator && Missing(configuration, "Sms:InternalApiKey"))
        {
            logger.LogWarning(
                "Production has Sms:AllowInternalSimulator enabled without Sms__InternalApiKey. The internal simulator is disabled until a key is set.");
        }
    }

    private static bool Missing(IConfiguration configuration, string key) =>
        string.IsNullOrWhiteSpace(configuration[key]);
}
