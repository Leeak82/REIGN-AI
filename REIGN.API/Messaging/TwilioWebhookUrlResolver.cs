namespace REIGN.API.Messaging;

/// <summary>
/// Builds the public URLs Twilio may have signed. Behind Render (and other TLS
/// proxies) <c>Request.Scheme</c>/<c>Request.Host</c> are the internal listen
/// address (<c>http://[::]:10000</c>), not the URL configured on the Twilio number.
/// </summary>
public static class TwilioWebhookUrlResolver
{
    public const string WebhookPath = "/api/sms/webhooks/twilio";

    public static IReadOnlyList<string> Candidates(
        string? requestScheme,
        string? requestHost,
        string? requestPath,
        string? requestQuery,
        string? webhookPublicUrl,
        string? publicBaseUrl,
        string? forwardedProto = null,
        string? forwardedHost = null,
        string? renderExternalUrl = null)
    {
        var path = string.IsNullOrWhiteSpace(requestPath) ? WebhookPath : requestPath;
        var query = NormalizeQuery(requestQuery);
        var urls = new List<string>();

        AddConfigured(urls, webhookPublicUrl, path, query);
        AddConfigured(urls, publicBaseUrl, path, query);
        AddConfigured(urls, renderExternalUrl, path, query);

        var proto = FirstHeaderValue(forwardedProto);
        var host = FirstHeaderValue(forwardedHost);
        if (!string.IsNullOrWhiteSpace(host))
        {
            var scheme = string.IsNullOrWhiteSpace(proto) ? "https" : proto;
            AddRaw(urls, $"{scheme}://{host}{path}{query}");
        }

        if (!string.IsNullOrWhiteSpace(requestHost))
        {
            var scheme = string.IsNullOrWhiteSpace(requestScheme) ? "https" : requestScheme;
            AddRaw(urls, $"{scheme}://{requestHost}{path}{query}");
        }

        return urls;
    }

    private static string NormalizeQuery(string? query)
    {
        if (string.IsNullOrWhiteSpace(query) || query == "?")
        {
            return "";
        }

        return query.StartsWith('?') ? query : "?" + query;
    }

    private static string? FirstHeaderValue(string? header)
    {
        if (string.IsNullOrWhiteSpace(header))
        {
            return null;
        }

        var comma = header.IndexOf(',');
        var value = (comma >= 0 ? header[..comma] : header).Trim();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static void AddConfigured(List<string> urls, string? configured, string path, string query)
    {
        if (string.IsNullOrWhiteSpace(configured))
        {
            return;
        }

        var value = configured.Trim();
        if (IsOriginOnly(value))
        {
            AddRaw(urls, value.TrimEnd('/') + path + query);
            return;
        }

        if (!value.Contains('?', StringComparison.Ordinal) && query.Length > 0)
        {
            AddRaw(urls, value + query);
            return;
        }

        AddRaw(urls, value);
    }

    private static bool IsOriginOnly(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return false;
        }

        return uri.AbsolutePath is "" or "/";
    }

    private static void AddRaw(List<string> urls, string url)
    {
        foreach (var variant in Variants(url))
        {
            if (!urls.Contains(variant, StringComparer.Ordinal))
            {
                urls.Add(variant);
            }
        }
    }

    private static IEnumerable<string> Variants(string url)
    {
        yield return url;

        if (TrySwapScheme(url, out var swapped))
        {
            yield return swapped;
        }

        if (TryToggleTrailingSlash(url, out var slashed))
        {
            yield return slashed;
            if (TrySwapScheme(slashed, out var swappedSlash))
            {
                yield return swappedSlash;
            }
        }
    }

    private static bool TrySwapScheme(string url, out string swapped)
    {
        swapped = "";
        if (url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            swapped = "http://" + url["https://".Length..];
            return true;
        }

        if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
        {
            swapped = "https://" + url["http://".Length..];
            return true;
        }

        return false;
    }

    private static bool TryToggleTrailingSlash(string url, out string toggled)
    {
        toggled = "";
        var queryIndex = url.IndexOf('?', StringComparison.Ordinal);
        var pathPart = queryIndex >= 0 ? url[..queryIndex] : url;
        var queryPart = queryIndex >= 0 ? url[queryIndex..] : "";

        if (pathPart.EndsWith('/'))
        {
            var trimmed = pathPart.TrimEnd('/');
            if (trimmed.Equals("http:/", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Equals("https:/", StringComparison.OrdinalIgnoreCase) ||
                !trimmed.Contains('/', StringComparison.Ordinal))
            {
                return false;
            }

            toggled = trimmed + queryPart;
            return toggled != url;
        }

        toggled = pathPart + "/" + queryPart;
        return true;
    }
}
