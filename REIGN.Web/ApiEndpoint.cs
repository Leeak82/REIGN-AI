namespace REIGN.Web;

public static class ApiEndpoint
{
    public static string Resolve(IConfiguration configuration)
    {
        // Explicit host alias must override the checked-in localhost defaults.
        var value = new[] { configuration["REIGN_API_BASE_URL"],
            configuration["ReignApi:BaseUrl"], configuration["ApiBaseUrl"] }
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))
            ?? "http://localhost:5012/";
        if (!Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            || !string.IsNullOrEmpty(uri.UserInfo)
            || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
            throw new InvalidOperationException("REIGN API base URL must be an absolute HTTP(S) URL without credentials, query, or fragment.");
        return uri.AbsoluteUri.TrimEnd('/') + "/";
    }
}
