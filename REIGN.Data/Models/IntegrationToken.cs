namespace REIGN.Data.Models;

public class IntegrationToken
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Provider { get; set; } = "";

    public string AccessToken { get; set; } = "";

    public string RefreshToken { get; set; } = "";

    public DateTimeOffset? AccessTokenExpiresAt { get; set; }

    public string? TokenType { get; set; }

    public string? Scope { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
