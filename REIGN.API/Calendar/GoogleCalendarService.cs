using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using REIGN.API.Options;
using REIGN.Data;
using REIGN.Data.Models;

namespace REIGN.API.Calendar;

public class GoogleCalendarService : ICalendarService
{
    public const string ProviderKey = "GoogleCalendar";

    private readonly HttpClient _http;
    private readonly GoogleCalendarOptions _options;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<GoogleCalendarService> _logger;

    public GoogleCalendarService(
        HttpClient http,
        IOptions<GoogleCalendarOptions> options,
        IServiceScopeFactory scopeFactory,
        ILogger<GoogleCalendarService> logger)
    {
        _http = http;
        _options = options.Value;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public string ProviderName => "Google";

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(_options.ClientId) &&
        !string.IsNullOrWhiteSpace(_options.ClientSecret);

    public bool IsSimulated => false;

    public bool HasStoredGrant
    {
        get
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ReignDbContext>();
            return db.IntegrationTokens.AsNoTracking().Any(x => x.Provider == ProviderKey && x.RefreshToken != "");
        }
    }

    public async Task<CalendarSyncResult> UpsertAppointmentAsync(CalendarEventRequest request, CancellationToken cancellationToken = default)
    {
        var access = await GetAccessTokenAsync(cancellationToken);
        if (access.Error != null)
        {
            return CalendarSyncResult.Fail(ProviderName, access.Error);
        }

        var timeZoneId = string.IsNullOrWhiteSpace(_options.TimeZone) ? "America/New_York" : _options.TimeZone;
        var tz = CalendarTime.Resolve(timeZoneId);
        var payload = new Dictionary<string, object?>
        {
            ["summary"] = request.Summary,
            ["description"] = request.Description,
            ["start"] = new Dictionary<string, string>
            {
                ["dateTime"] = CalendarTime.ToWallClockRfc3339(request.Start, tz),
                ["timeZone"] = timeZoneId
            },
            ["end"] = new Dictionary<string, string>
            {
                ["dateTime"] = CalendarTime.ToWallClockRfc3339(request.End, tz),
                ["timeZone"] = timeZoneId
            },
            ["status"] = request.Status.Equals("Cancelled", StringComparison.OrdinalIgnoreCase)
                ? "cancelled"
                : "confirmed",
            ["extendedProperties"] = new Dictionary<string, object>
            {
                ["private"] = new Dictionary<string, string>
                {
                    ["reignAppointmentId"] = request.AppointmentId.ToString()
                }
            }
        };

        var calendarId = Uri.EscapeDataString(string.IsNullOrWhiteSpace(_options.CalendarId) ? "primary" : _options.CalendarId);
        var eventId = request.ExistingEventId;
        if (string.IsNullOrWhiteSpace(eventId))
        {
            eventId = await FindExistingEventIdAsync(access.Token!, request.AppointmentId, calendarId, cancellationToken);
        }

        HttpRequestMessage message;
        if (string.IsNullOrWhiteSpace(eventId))
        {
            message = new HttpRequestMessage(
                HttpMethod.Post,
                $"https://www.googleapis.com/calendar/v3/calendars/{calendarId}/events");
        }
        else
        {
            message = new HttpRequestMessage(
                HttpMethod.Put,
                $"https://www.googleapis.com/calendar/v3/calendars/{calendarId}/events/{Uri.EscapeDataString(eventId)}");
        }

        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", access.Token);
        message.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        try
        {
            using var response = await _http.SendAsync(message, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Google Calendar upsert failed: {Status} {Body}", (int)response.StatusCode, Truncate(body));
                return CalendarSyncResult.Fail(ProviderName, $"Google Calendar HTTP {(int)response.StatusCode}");
            }

            using var doc = JsonDocument.Parse(body);
            var id = doc.RootElement.TryGetProperty("id", out var idEl) ? idEl.GetString() : eventId;
            return CalendarSyncResult.Ok(ProviderName, id);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Google Calendar upsert threw");
            return CalendarSyncResult.Fail(ProviderName, ex.Message);
        }
    }

    public async Task<CalendarSyncResult> CancelAppointmentAsync(string? eventId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(eventId))
        {
            return CalendarSyncResult.Ok(ProviderName, null);
        }

        var access = await GetAccessTokenAsync(cancellationToken);
        if (access.Error != null)
        {
            return CalendarSyncResult.Fail(ProviderName, access.Error);
        }

        var calendarId = Uri.EscapeDataString(string.IsNullOrWhiteSpace(_options.CalendarId) ? "primary" : _options.CalendarId);
        using var message = new HttpRequestMessage(
            HttpMethod.Delete,
            $"https://www.googleapis.com/calendar/v3/calendars/{calendarId}/events/{Uri.EscapeDataString(eventId)}");
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", access.Token);

        try
        {
            using var response = await _http.SendAsync(message, cancellationToken);
            if (response.IsSuccessStatusCode || response.StatusCode == System.Net.HttpStatusCode.Gone ||
                response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return CalendarSyncResult.Ok(ProviderName, eventId);
            }

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning("Google Calendar delete failed: {Status} {Body}", (int)response.StatusCode, Truncate(body));
            return CalendarSyncResult.Fail(ProviderName, $"Google Calendar HTTP {(int)response.StatusCode}");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Google Calendar delete threw");
            return CalendarSyncResult.Fail(ProviderName, ex.Message);
        }
    }

    private async Task<(string? Token, string? Error)> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        if (!IsConfigured)
        {
            return (null, "Google Calendar ClientId/ClientSecret are not configured.");
        }

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ReignDbContext>();
        var token = await db.IntegrationTokens.FirstOrDefaultAsync(x => x.Provider == ProviderKey, cancellationToken);
        if (token == null || string.IsNullOrWhiteSpace(token.RefreshToken))
        {
            return (null, "Google Calendar is not connected. Visit /api/integrations/google/authorize after setting ClientId and ClientSecret.");
        }

        if (!string.IsNullOrWhiteSpace(token.AccessToken) &&
            token.AccessTokenExpiresAt is { } expires &&
            expires > DateTimeOffset.UtcNow.AddMinutes(5))
        {
            return (token.AccessToken, null);
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://oauth2.googleapis.com/token");
        request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = _options.ClientId,
            ["client_secret"] = _options.ClientSecret,
            ["refresh_token"] = token.RefreshToken,
            ["grant_type"] = "refresh_token"
        });

        using var response = await _http.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return (null, "Google OAuth refresh failed. Reconnect calendar.");
        }

        using var doc = JsonDocument.Parse(body);
        var accessToken = doc.RootElement.GetProperty("access_token").GetString();
        var expiresIn = doc.RootElement.TryGetProperty("expires_in", out var expEl) ? expEl.GetInt32() : 3600;
        token.AccessToken = accessToken ?? "";
        if (doc.RootElement.TryGetProperty("refresh_token", out var rotated) &&
            rotated.ValueKind == JsonValueKind.String &&
            !string.IsNullOrWhiteSpace(rotated.GetString()))
        {
            token.RefreshToken = rotated.GetString() ?? token.RefreshToken;
        }

        token.AccessTokenExpiresAt = DateTimeOffset.UtcNow.AddSeconds(expiresIn);
        token.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return (token.AccessToken, null);
    }

    private async Task<string?> FindExistingEventIdAsync(
        string accessToken,
        Guid appointmentId,
        string calendarId,
        CancellationToken cancellationToken)
    {
        try
        {
            var query = Uri.EscapeDataString($"reignAppointmentId={appointmentId}");
            using var message = new HttpRequestMessage(
                HttpMethod.Get,
                $"https://www.googleapis.com/calendar/v3/calendars/{calendarId}/events?privateExtendedProperty={query}&maxResults=1&showDeleted=false");
            message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            using var response = await _http.SendAsync(message, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            if (!doc.RootElement.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            foreach (var item in items.EnumerateArray())
            {
                if (item.TryGetProperty("id", out var idEl) && idEl.ValueKind == JsonValueKind.String)
                {
                    var id = idEl.GetString();
                    if (!string.IsNullOrWhiteSpace(id))
                    {
                        return id;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Google Calendar duplicate lookup failed for {AppointmentId}", appointmentId);
        }

        return null;
    }

    public async Task StoreAuthorizationCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://oauth2.googleapis.com/token");
        request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["code"] = code,
            ["client_id"] = _options.ClientId,
            ["client_secret"] = _options.ClientSecret,
            ["redirect_uri"] = _options.RedirectUri,
            ["grant_type"] = "authorization_code"
        });

        using var response = await _http.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException("Google OAuth code exchange failed.");
        }

        using var doc = JsonDocument.Parse(body);
        var accessToken = doc.RootElement.GetProperty("access_token").GetString() ?? "";
        var refreshToken = doc.RootElement.TryGetProperty("refresh_token", out var refreshEl)
            ? refreshEl.GetString() ?? ""
            : "";
        var expiresIn = doc.RootElement.TryGetProperty("expires_in", out var expEl) ? expEl.GetInt32() : 3600;
        var scope = doc.RootElement.TryGetProperty("scope", out var scopeEl) ? scopeEl.GetString() : null;
        var tokenType = doc.RootElement.TryGetProperty("token_type", out var typeEl) ? typeEl.GetString() : "Bearer";

        using var dbScope = _scopeFactory.CreateScope();
        var db = dbScope.ServiceProvider.GetRequiredService<ReignDbContext>();
        var existing = await db.IntegrationTokens.FirstOrDefaultAsync(x => x.Provider == ProviderKey, cancellationToken);
        if (existing == null)
        {
            existing = new IntegrationToken { Id = Guid.NewGuid(), Provider = ProviderKey };
            db.IntegrationTokens.Add(existing);
        }

        existing.AccessToken = accessToken;
        if (!string.IsNullOrWhiteSpace(refreshToken))
        {
            existing.RefreshToken = refreshToken;
        }

        existing.AccessTokenExpiresAt = DateTimeOffset.UtcNow.AddSeconds(expiresIn);
        existing.Scope = scope;
        existing.TokenType = tokenType;
        existing.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }

    private static string Truncate(string value) =>
        value.Length <= 300 ? value : value[..300];
}
