using System.Security.Cryptography;
using System.Text;

namespace REIGN.API.Messaging;

/// <summary>
/// Validates Twilio webhook signatures per
/// https://www.twilio.com/docs/usage/webhooks/webhooks-security
/// without requiring the Twilio SDK at build time.
/// </summary>
public static class TwilioRequestValidator
{
    public static bool IsValid(
        string authToken,
        string requestUrl,
        IDictionary<string, string> parameters,
        string? expectedSignature)
    {
        if (string.IsNullOrWhiteSpace(authToken) ||
            string.IsNullOrWhiteSpace(requestUrl) ||
            string.IsNullOrWhiteSpace(expectedSignature))
        {
            return false;
        }

        var builder = new StringBuilder(requestUrl);
        foreach (var pair in parameters.OrderBy(p => p.Key, StringComparer.Ordinal))
        {
            builder.Append(pair.Key).Append(pair.Value);
        }

        using var hmac = new HMACSHA1(Encoding.UTF8.GetBytes(authToken));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(builder.ToString()));
        var computed = Convert.ToBase64String(hash);

        var expectedBytes = Encoding.UTF8.GetBytes(expectedSignature);
        var computedBytes = Encoding.UTF8.GetBytes(computed);
        return expectedBytes.Length == computedBytes.Length &&
               CryptographicOperations.FixedTimeEquals(expectedBytes, computedBytes);
    }

    public static bool IsValidAny(
        string authToken,
        IEnumerable<string> requestUrls,
        IDictionary<string, string> parameters,
        string? expectedSignature,
        out string? matchedUrl)
    {
        matchedUrl = null;
        foreach (var url in requestUrls)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                continue;
            }

            if (IsValid(authToken, url, parameters, expectedSignature))
            {
                matchedUrl = url;
                return true;
            }
        }

        return false;
    }

    public static string ComputeSignature(
        string authToken,
        string requestUrl,
        IDictionary<string, string> parameters)
    {
        var builder = new StringBuilder(requestUrl);
        foreach (var pair in parameters.OrderBy(p => p.Key, StringComparer.Ordinal))
        {
            builder.Append(pair.Key).Append(pair.Value);
        }

        using var hmac = new HMACSHA1(Encoding.UTF8.GetBytes(authToken));
        return Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(builder.ToString())));
    }
}
