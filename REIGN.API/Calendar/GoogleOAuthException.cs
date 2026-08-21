namespace REIGN.API.Calendar;

/// <summary>
/// Token endpoint failure. Message, <see cref="GoogleError"/>, and
/// <see cref="GoogleErrorDescription"/> are already sanitized (no secrets).
/// </summary>
public sealed class GoogleOAuthException : InvalidOperationException
{
    public GoogleOAuthException(
        string message,
        int? httpStatus,
        string? googleError,
        string? googleErrorDescription,
        string redirectUri)
        : base(message)
    {
        HttpStatus = httpStatus;
        GoogleError = googleError;
        GoogleErrorDescription = googleErrorDescription;
        RedirectUri = redirectUri;
    }

    public int? HttpStatus { get; }

    public string? GoogleError { get; }

    public string? GoogleErrorDescription { get; }

    public string RedirectUri { get; }
}
