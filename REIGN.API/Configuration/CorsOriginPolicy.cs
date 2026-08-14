using Microsoft.Extensions.Configuration;

namespace REIGN.API.Configuration;

/// <summary>
/// Resolves browser CORS origins. Development always includes localhost.
/// Production uses only configured origins and never allows a wildcard.
/// </summary>
public static class CorsOriginPolicy
{
    public const string ConfigurationKey = "Cors:AllowedOrigins";
    public const string PolicyName = "Reign";

    public static readonly string[] DevelopmentLocalhosts =
    [
        "http://localhost:5000",
        "https://localhost:5000",
        "http://localhost:5001",
        "https://localhost:5001",
        "http://localhost:5012",
        "https://localhost:5012",
        "http://127.0.0.1:5000",
        "https://127.0.0.1:5000",
        "http://127.0.0.1:5001",
        "https://127.0.0.1:5001",
        "http://127.0.0.1:5012",
        "https://127.0.0.1:5012"
    ];

    public static CorsOriginResolution Resolve(IConfiguration configuration, bool isDevelopment)
    {
        var origins = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var rejectedWildcard = false;

        if (isDevelopment)
        {
            foreach (var localhost in DevelopmentLocalhosts)
            {
                Add(origins, seen, localhost);
            }
        }

        foreach (var raw in Split(configuration[ConfigurationKey]))
        {
            if (raw == "*")
            {
                rejectedWildcard = true;
                continue;
            }

            if (!TryNormalizeOrigin(raw, out var origin))
            {
                continue;
            }

            Add(origins, seen, origin);
        }

        return new CorsOriginResolution(origins, rejectedWildcard);
    }

    public static IEnumerable<string> Split(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            yield break;
        }

        foreach (var part in value.Split([',', ';', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = part.Trim();
            if (trimmed.Length > 0)
            {
                yield return trimmed;
            }
        }
    }

    public static bool TryNormalizeOrigin(string value, out string origin)
    {
        origin = "";
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri))
        {
            return false;
        }

        if (uri.Scheme is not ("http" or "https"))
        {
            return false;
        }

        origin = $"{uri.Scheme}://{uri.Authority}";
        return true;
    }

    private static void Add(List<string> origins, HashSet<string> seen, string origin)
    {
        if (seen.Add(origin))
        {
            origins.Add(origin);
        }
    }
}

public sealed record CorsOriginResolution(IReadOnlyList<string> Origins, bool RejectedWildcard);
