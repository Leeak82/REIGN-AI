using Microsoft.Extensions.Hosting;
using REIGN.API.Options;

namespace REIGN.API.Configuration;

/// <summary>
/// Resolves <c>GoogleCalendar:RedirectUri</c> from the providers that actually win at runtime.
///
/// ASP.NET Core order after <see cref="WebApplication.CreateBuilder"/>:
/// appsettings.json, then environment variables (<c>GoogleCalendar__RedirectUri</c>), then
/// later in-memory sources. <c>GOOGLE_REDIRECT_URI</c> is NOT a nested key, so it does not
/// bind onto <see cref="GoogleCalendarOptions"/> unless copied.
///
/// Local Docker publishes :8080. A gitignored host <c>.env</c> used for <c>dotnet run</c>
/// often sets <c>GOOGLE_REDIRECT_URI=https://localhost:5001/...</c>. Compose interpolation
/// of that variable used to copy the Kestrel callback into the container, which looks like
/// appsettings "winning" because both values are the 5001 URI.
/// </summary>
public static class GoogleRedirectUri
{
    public const string ConfigurationKey = "GoogleCalendar:RedirectUri";

    public const string NestedEnvironmentName = "GoogleCalendar__RedirectUri";

    public const string DockerCallback = "http://localhost:8080/api/integrations/google/callback";

    public const string KestrelHttpsCallback = "https://localhost:5001/api/integrations/google/callback";

    public static void Apply(ConfigurationManager configuration, IHostEnvironment environment)
    {
        var resolved = Resolve(configuration[ConfigurationKey], environment.IsDevelopment());
        if (string.IsNullOrWhiteSpace(resolved))
        {
            return;
        }

        if (!string.Equals(configuration[ConfigurationKey], resolved, StringComparison.Ordinal))
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                [ConfigurationKey] = resolved
            });
        }
    }

    public static void ApplyToOptions(GoogleCalendarOptions options, IHostEnvironment environment)
    {
        var resolved = Resolve(options.RedirectUri, environment.IsDevelopment());
        if (!string.IsNullOrWhiteSpace(resolved))
        {
            options.RedirectUri = resolved;
        }
    }

    public static string Resolve(string? configured, bool isDevelopment)
    {
        var nested = NullIfWhiteSpace(Environment.GetEnvironmentVariable(NestedEnvironmentName));
        var alias = NullIfWhiteSpace(Environment.GetEnvironmentVariable("GOOGLE_REDIRECT_URI"))
            ?? NullIfWhiteSpace(Environment.GetEnvironmentVariable("GOOGLE_CALENDAR_REDIRECT_URI"));
        var current = NullIfWhiteSpace(configured);
        var resolved = nested ?? alias ?? current ?? "";

        if (ShouldRewriteKestrelCallbackInContainer(resolved, isDevelopment))
        {
            return DockerCallback;
        }

        return resolved;
    }

    public static bool ShouldRewriteKestrelCallbackInContainer(string resolved, bool isDevelopment) =>
        isDevelopment
        && RunningInContainer()
        && (string.IsNullOrWhiteSpace(resolved)
            || resolved.Contains("localhost:5001", StringComparison.OrdinalIgnoreCase));

    public static bool RunningInContainer()
    {
        var flag = Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER");
        return string.Equals(flag, "true", StringComparison.OrdinalIgnoreCase)
            || string.Equals(flag, "1", StringComparison.OrdinalIgnoreCase);
    }

    private static string? NullIfWhiteSpace(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
