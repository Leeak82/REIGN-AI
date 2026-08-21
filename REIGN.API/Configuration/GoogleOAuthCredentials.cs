namespace REIGN.API.Configuration;

/// <summary>
/// Normalizes OAuth client values copied from Google Cloud Console or a host env UI.
/// Surrounding quotes and whitespace are common paste artifacts and cause
/// <c>invalid_client</c> on the token endpoint.
/// </summary>
public static class GoogleOAuthCredentials
{
    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "";
        }

        var trimmed = value.Trim();
        if (trimmed.Length >= 2 && trimmed[0] == '"' && trimmed[^1] == '"')
        {
            trimmed = trimmed[1..^1].Trim();
        }

        return trimmed;
    }

    public static bool LooksLikeWebClientSecret(string? value) =>
        Normalize(value).StartsWith("GOCSPX-", StringComparison.Ordinal);
}
