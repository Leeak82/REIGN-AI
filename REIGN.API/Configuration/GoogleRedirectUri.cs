using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using REIGN.API.Options;

namespace REIGN.API.Configuration;

/// <summary>
/// Resolves <c>GoogleCalendar:RedirectUri</c> from the providers that actually win at runtime.
///
/// ASP.NET Core order after <see cref="WebApplication.CreateBuilder"/>:
/// appsettings.json, then environment variables (<c>GoogleCalendar__RedirectUri</c>), then
/// later in-memory sources. <c>GOOGLE_REDIRECT_URI</c> is not a nested key, so it does not
/// bind onto <see cref="GoogleCalendarOptions"/> unless copied by
/// <see cref="ConfigEnvironmentAliases"/>.
///
/// Local Docker publishes :8080. A gitignored host <c>.env</c> used for <c>dotnet run</c>
/// often sets <c>GOOGLE_REDIRECT_URI=https://localhost:5001/...</c>. Compose interpolation
/// of that variable used to copy the Kestrel callback into the container as
/// <c>GoogleCalendar__RedirectUri</c>, which then won over appsettings — with the same 5001
/// value, so it looked like appsettings was winning.
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

        configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            [ConfigurationKey] = resolved
        });
    }

    public static void ApplyToOptions(GoogleCalendarOptions options, IHostEnvironment environment)
    {
        var resolved = Resolve(options.RedirectUri, environment.IsDevelopment());
        if (!string.IsNullOrWhiteSpace(resolved))
        {
            options.RedirectUri = resolved;
        }
    }

    /// <summary>
    /// Last-mile callback used by authorize, status, and token exchange.
    /// A Development request served on localhost:8080 always uses
    /// <see cref="DockerCallback"/>, even if IOptions still holds the Kestrel 5001 default
    /// from <c>appsettings.json</c>.
    /// </summary>
    public static string ResolveForRequest(string? configured, bool isDevelopment, HttpRequest? request)
    {
        if (isDevelopment && (RunningInContainer() || IsDockerPublishedHost(request)))
        {
            return DockerCallback;
        }

        return NullIfWhiteSpace(configured) ?? "";
    }

    public static string Resolve(string? configured, bool isDevelopment)
    {
        if (isDevelopment && RunningInContainer())
        {
            return DockerCallback;
        }

        var nested = NullIfWhiteSpace(Environment.GetEnvironmentVariable(NestedEnvironmentName));
        var alias = NullIfWhiteSpace(Environment.GetEnvironmentVariable("GOOGLE_REDIRECT_URI"))
            ?? NullIfWhiteSpace(Environment.GetEnvironmentVariable("GOOGLE_CALENDAR_REDIRECT_URI"));
        var current = NullIfWhiteSpace(configured);
        return nested ?? alias ?? current ?? "";
    }

    public static bool IsDockerPublishedHost(HttpRequest? request)
    {
        if (request?.Host.HasValue != true)
        {
            return false;
        }

        var name = request.Host.Host;
        if (!name.Equals("localhost", StringComparison.OrdinalIgnoreCase)
            && !name.Equals("127.0.0.1", StringComparison.Ordinal))
        {
            return false;
        }

        return request.Host.Port is 8080;
    }

    public static bool RunningInContainer()
    {
        var flag = Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER");
        return string.Equals(flag, "true", StringComparison.OrdinalIgnoreCase)
            || string.Equals(flag, "1", StringComparison.OrdinalIgnoreCase);
    }

    private static string? NullIfWhiteSpace(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
