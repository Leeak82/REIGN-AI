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
/// Public hosts (Render, Railway, Azure) must never send a localhost callback.
/// When <c>RENDER_EXTERNAL_URL</c> / <c>RENDER_EXTERNAL_HOSTNAME</c> or a public
/// request host is available, leftover localhost values resolve to that origin.
/// </summary>
public static class GoogleRedirectUri
{
    public const string ConfigurationKey = "GoogleCalendar:RedirectUri";

    public const string NestedEnvironmentName = "GoogleCalendar__RedirectUri";

    public const string DockerFlagName = "REIGN_DOCKER";

    public const string CallbackPath = "/api/integrations/google/callback";

    public const string DockerCallback = "http://localhost:8080" + CallbackPath;

    public const string KestrelHttpsCallback = "https://localhost:5001" + CallbackPath;

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
        options.ClientId = GoogleOAuthCredentials.Normalize(options.ClientId);
        options.ClientSecret = GoogleOAuthCredentials.Normalize(options.ClientSecret);

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
    /// Public production hosts win over localhost leftovers, including accidental
    /// <c>REIGN_DOCKER=1</c> on Render. An already-public callback is never rewritten
    /// to Docker localhost just because the image listens on :8080.
    /// </summary>
    public static string EnsureOAuthCallback(string? configured, HttpRequest? request = null, bool? isDevelopment = null)
    {
        var current = NullIfWhiteSpace(configured);
        var nested = NullIfWhiteSpace(Environment.GetEnvironmentVariable(NestedEnvironmentName));
        var alias = NullIfWhiteSpace(Environment.GetEnvironmentVariable("GOOGLE_REDIRECT_URI"))
            ?? NullIfWhiteSpace(Environment.GetEnvironmentVariable("GOOGLE_CALENDAR_REDIRECT_URI"));
        var candidate = current ?? nested ?? alias ?? "";

        var platformPublic = TryPlatformPublicCallback();
        if (ShouldUseResolvedPublic(candidate, request) && platformPublic != null)
        {
            return platformPublic;
        }

        var requestPublic = TryRequestPublicCallback(request);
        if (ShouldUseResolvedPublic(candidate, request) && requestPublic != null)
        {
            return requestPublic;
        }

        // Keep https://your-host/... even when REIGN_DOCKER=1 or ASPNETCORE_URLS=:8080.
        // Replacing that with http://localhost:8080 is what makes token exchange
        // send a different redirect_uri than the authorize request Google already accepted.
        if (IsPublicCallback(candidate))
        {
            if ((isDevelopment ?? false) && RunningInContainer())
            {
                return DockerCallback;
            }

            return ToCallback(candidate);
        }

        if (ForcedDockerDeployment() || IsDockerPublishedHost(request))
        {
            return DockerCallback;
        }

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

    public static string? TryPublicCallback(HttpRequest? request = null) =>
        TryPlatformPublicCallback() ?? TryRequestPublicCallback(request);

    public static string? TryPlatformPublicCallback()
    {
        var renderUrl = NullIfWhiteSpace(Environment.GetEnvironmentVariable("RENDER_EXTERNAL_URL"));
        if (renderUrl != null)
        {
            return ToCallback(renderUrl);
        }

        var renderHost = NullIfWhiteSpace(Environment.GetEnvironmentVariable("RENDER_EXTERNAL_HOSTNAME"));
        return renderHost == null ? null : ToCallback("https://" + renderHost);
    }

    public static string? TryRequestPublicCallback(HttpRequest? request)
    {
        var publicOrigin = PublicRequestOrigin(request);
        return publicOrigin == null ? null : ToCallback(publicOrigin);
    }

    public static bool IsPublicCallback(string? configured)
    {
        if (string.IsNullOrWhiteSpace(configured) || LooksLikeLocalCallback(configured))
        {
            return false;
        }

        if (!Uri.TryCreate(configured, UriKind.Absolute, out var uri))
        {
            return false;
        }

        return IsUsablePublicHost(uri.Host);
    }

    public static string ToCallback(string origin)
    {
        var trimmed = origin.Trim().TrimEnd('/');
        if (trimmed.EndsWith(CallbackPath, StringComparison.OrdinalIgnoreCase))
        {
            return trimmed;
        }

        return trimmed + CallbackPath;
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

    public static bool LooksLikeLocalCallback(string? configured) =>
        !string.IsNullOrWhiteSpace(configured)
        && (configured.Contains("localhost", StringComparison.OrdinalIgnoreCase)
            || configured.Contains("127.0.0.1", StringComparison.Ordinal)
            || configured.Contains("[::1]", StringComparison.OrdinalIgnoreCase)
            || configured.Contains("0.0.0.0", StringComparison.Ordinal)
            || configured.Contains("://[::]", StringComparison.OrdinalIgnoreCase));

    public static bool IsDockerPublishedHost(HttpRequest? request)
    {
        if (request == null)
        {
            return false;
        }

        var hostName = request.Host.HasValue ? request.Host.Host : null;
        var bindHost = IsLoopbackHost(hostName) || IsUnspecifiedHost(hostName);
        var localPort = request.HttpContext?.Connection.LocalPort;
        if (localPort == 8080 && bindHost)
        {
            return true;
        }

        if (request.Host.HasValue && bindHost && request.Host.Port is 8080)
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

    private static string? PublicRequestOrigin(HttpRequest? request)
    {
        if (request == null)
        {
            return null;
        }

        var host = FirstForwardedValue(request, "X-Forwarded-Host")
            ?? (request.Host.HasValue ? request.Host.Value : null);
        if (string.IsNullOrWhiteSpace(host))
        {
            return null;
        }

        var hostName = host.Split(',')[0].Trim();
        var nameOnly = HostNameOnly(hostName);
        if (!IsUsablePublicHost(nameOnly))
        {
            return null;
        }

        var scheme = FirstForwardedValue(request, "X-Forwarded-Proto") ?? request.Scheme;
        if (string.IsNullOrWhiteSpace(scheme) || scheme.Equals("http", StringComparison.OrdinalIgnoreCase))
        {
            scheme = "https";
        }

        return scheme + "://" + hostName;
    }

    private static bool ShouldUseResolvedPublic(string candidate, HttpRequest? request) =>
        string.IsNullOrWhiteSpace(candidate)
        || LooksLikeLocalCallback(candidate)
        || ForcedDockerDeployment()
        || IsDockerPublishedHost(request);

    private static string HostNameOnly(string hostValue)
    {
        if (hostValue.StartsWith('[')
        {
            var end = hostValue.IndexOf(']');
            return end > 1 ? hostValue[1..end] : hostValue;
        }

        var colon = hostValue.LastIndexOf(':');
        if (colon > 0 && hostValue[(colon + 1)..].All(char.IsDigit))
        {
            return hostValue[..colon];
        }

        return hostValue;
    }

    public static bool IsUsablePublicHost(string? name)
    {
        if (string.IsNullOrWhiteSpace(name) || IsLoopbackHost(name) || IsUnspecifiedHost(name))
        {
            return false;
        }

        if (System.Net.IPAddress.TryParse(name, out _))
        {
            return false;
        }

        return name.Contains('.', StringComparison.Ordinal);
    }

    private static string? FirstForwardedValue(HttpRequest request, string header)
    {
        if (!request.Headers.TryGetValue(header, out var values))
        {
            return null;
        }

        var raw = values.ToString();
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        return raw.Split(',')[0].Trim();
    }

    private static bool IsLoopbackHost(string? name) =>
        !string.IsNullOrWhiteSpace(name)
        && (name.Equals("localhost", StringComparison.OrdinalIgnoreCase)
            || name.Equals("127.0.0.1", StringComparison.Ordinal)
            || name.Equals("::1", StringComparison.Ordinal)
            || name.Equals("[::1]", StringComparison.OrdinalIgnoreCase));

    private static bool IsUnspecifiedHost(string? name) =>
        !string.IsNullOrWhiteSpace(name)
        && (name.Equals("0.0.0.0", StringComparison.Ordinal)
            || name.Equals("::", StringComparison.Ordinal)
            || name.Equals("[::]", StringComparison.OrdinalIgnoreCase)
            || name.Equals("+", StringComparison.Ordinal)
            || name.Equals("*", StringComparison.Ordinal));

    private static string? NullIfWhiteSpace(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
