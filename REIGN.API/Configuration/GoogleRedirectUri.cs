using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using REIGN.API.Options;

namespace REIGN.API.Configuration;

/// <summary>
/// Resolves <c>GoogleCalendar:RedirectUri</c> for authorize and token exchange.
///
/// Local Docker (compose sets <c>REIGN_DOCKER=1</c> and publishes :8080) must use
/// <see cref="DockerCallback"/>. A leftover Kestrel
/// <c>https://localhost:5001/...</c> value from appsettings.json or a host .env
/// must never be sent to Google from that runtime.
///
/// Non-Docker <c>dotnet run</c> on https://localhost:5001 is unchanged.
/// Production public callbacks are unchanged.
/// </summary>
public static class GoogleRedirectUri
{
    public const string ConfigurationKey = "GoogleCalendar:RedirectUri";

    public const string NestedEnvironmentName = "GoogleCalendar__RedirectUri";

    public const string DockerFlagName = "REIGN_DOCKER";

    public const string DockerCallback = "http://localhost:8080/api/integrations/google/callback";

    public const string KestrelHttpsCallback = "https://localhost:5001/api/integrations/google/callback";

    public static void Apply(ConfigurationManager configuration, IHostEnvironment environment)
    {
        var resolved = EnsureOAuthCallback(configuration[ConfigurationKey], request: null, environment.IsDevelopment());
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
        var resolved = EnsureOAuthCallback(options.RedirectUri, request: null, environment.IsDevelopment());
        if (!string.IsNullOrWhiteSpace(resolved))
        {
            options.RedirectUri = resolved;
        }
    }

    public static string ResolveForRequest(string? configured, bool isDevelopment, HttpRequest? request) =>
        EnsureOAuthCallback(configured, request, isDevelopment);

    public static string Resolve(string? configured, bool isDevelopment) =>
        EnsureOAuthCallback(configured, request: null, isDevelopment);

    /// <summary>
    /// Single choke point for the URI sent to Google (authorize Location and token POST).
    /// Prefer the caller-supplied value over process env so a host
    /// <c>GoogleCalendar__RedirectUri=...5001...</c> cannot override a correct 8080 option.
    /// </summary>
    public static string EnsureOAuthCallback(string? configured, HttpRequest? request = null, bool? isDevelopment = null)
    {
        if (ForcedDockerDeployment() || IsDockerPublishedHost(request))
        {
            return DockerCallback;
        }

        var current = NullIfWhiteSpace(configured);
        var nested = NullIfWhiteSpace(Environment.GetEnvironmentVariable(NestedEnvironmentName));
        var alias = NullIfWhiteSpace(Environment.GetEnvironmentVariable("GOOGLE_REDIRECT_URI"))
            ?? NullIfWhiteSpace(Environment.GetEnvironmentVariable("GOOGLE_CALENDAR_REDIRECT_URI"));
        var candidate = current ?? nested ?? alias ?? "";

        if (LooksLikeKestrelCallback(candidate) && (RunningInContainer() || ListeningOnPublishedDockerPort()))
        {
            return DockerCallback;
        }

        if ((isDevelopment ?? false) && RunningInContainer())
        {
            return DockerCallback;
        }

        return candidate;
    }

    public static bool ForcedDockerDeployment()
    {
        var flag = Environment.GetEnvironmentVariable(DockerFlagName);
        return string.Equals(flag, "1", StringComparison.OrdinalIgnoreCase)
            || string.Equals(flag, "true", StringComparison.OrdinalIgnoreCase);
    }

    public static bool LooksLikeKestrelCallback(string? configured) =>
        !string.IsNullOrWhiteSpace(configured)
        && configured.Contains("localhost:5001", StringComparison.OrdinalIgnoreCase);

    public static bool IsDockerPublishedHost(HttpRequest? request)
    {
        if (request == null)
        {
            return false;
        }

        var localPort = request.HttpContext?.Connection.LocalPort;
        if (localPort == 8080 && IsLoopbackHost(request.Host.Host))
        {
            return true;
        }

        if (request.Host.HasValue && IsLoopbackHost(request.Host.Host) && request.Host.Port is 8080)
        {
            return true;
        }

        return false;
    }

    public static bool ListeningOnPublishedDockerPort()
    {
        var ports = Environment.GetEnvironmentVariable("ASPNETCORE_HTTP_PORTS");
        if (string.Equals(ports, "8080", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var urls = Environment.GetEnvironmentVariable("ASPNETCORE_URLS");
        return !string.IsNullOrWhiteSpace(urls) && urls.Contains(":8080", StringComparison.Ordinal);
    }

    public static bool RunningInContainer()
    {
        var flag = Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER");
        return string.Equals(flag, "true", StringComparison.OrdinalIgnoreCase)
            || string.Equals(flag, "1", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsLoopbackHost(string? name) =>
        !string.IsNullOrWhiteSpace(name)
        && (name.Equals("localhost", StringComparison.OrdinalIgnoreCase)
            || name.Equals("127.0.0.1", StringComparison.Ordinal)
            || name.Equals("::1", StringComparison.Ordinal));

    private static string? NullIfWhiteSpace(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
