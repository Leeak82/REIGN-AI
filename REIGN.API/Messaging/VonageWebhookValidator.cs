using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace REIGN.API.Messaging;

/// <summary>
/// Validates Vonage Messages API signed webhooks (HS256 JWT in Authorization)
/// and classic SMS API `sig` signatures when configured.
/// </summary>
public static class VonageWebhookValidator
{
    public static bool TryValidateJwt(string signatureSecret, string? authorizationHeader, string rawBody)
    {
        if (string.IsNullOrWhiteSpace(signatureSecret) || string.IsNullOrWhiteSpace(authorizationHeader))
        {
            return false;
        }

        var token = authorizationHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            ? authorizationHeader["Bearer ".Length..].Trim()
            : authorizationHeader.Trim();

        var parts = token.Split('.');
        if (parts.Length != 3)
        {
            return false;
        }

        var headerJson = Encoding.UTF8.GetString(Base64UrlDecode(parts[0]));
        using var header = JsonDocument.Parse(headerJson);
        var alg = header.RootElement.TryGetProperty("alg", out var algEl) ? algEl.GetString() : null;
        if (!string.Equals(alg, "HS256", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var unsigned = parts[0] + "." + parts[1];
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(signatureSecret));
        var computed = hmac.ComputeHash(Encoding.UTF8.GetBytes(unsigned));
        var expected = Base64UrlDecode(parts[2]);
        if (computed.Length != expected.Length ||
            !CryptographicOperations.FixedTimeEquals(computed, expected))
        {
            return false;
        }

        var payloadJson = Encoding.UTF8.GetString(Base64UrlDecode(parts[1]));
        using var payload = JsonDocument.Parse(payloadJson);
        if (payload.RootElement.TryGetProperty("payload_hash", out var hashEl))
        {
            var claimed = hashEl.GetString();
            var actual = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawBody))).ToLowerInvariant();
            if (!string.Equals(claimed, actual, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    public static string CreateHs256Jwt(string signatureSecret, IDictionary<string, object> claims)
    {
        var header = Base64UrlEncode(Encoding.UTF8.GetBytes("""{"alg":"HS256","typ":"JWT"}"""));
        var payload = Base64UrlEncode(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(claims)));
        var unsigned = header + "." + payload;
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(signatureSecret));
        var signature = Base64UrlEncode(hmac.ComputeHash(Encoding.UTF8.GetBytes(unsigned)));
        return unsigned + "." + signature;
    }

    private static byte[] Base64UrlDecode(string input)
    {
        var padded = input.Replace('-', '+').Replace('_', '/');
        switch (padded.Length % 4)
        {
            case 2: padded += "=="; break;
            case 3: padded += "="; break;
        }

        return Convert.FromBase64String(padded);
    }

    private static string Base64UrlEncode(byte[] data)
    {
        return Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}
