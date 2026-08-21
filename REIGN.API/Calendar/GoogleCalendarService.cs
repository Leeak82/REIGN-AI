using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using REIGN.API.Configuration;
using REIGN.API.Options;
using REIGN.Data;
using REIGN.Data.Models;

namespace REIGN.API.Calendar;

public class GoogleCalendarService : ICalendarService
{
    public const string ProviderKey = "GoogleCalendar";

    public const string RequiredScope = "https://www.googleapis.com/auth/calendar.events";

    public static string BuildAuthorizationUrl(string clientId, string redirectUri) =>
        "https://accounts.google.com/o/oauth2/v2/auth" +
        $"?client_id={Uri.EscapeDataString(clientId)}" +
        $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
        "&response_type=code" +
        $"&scope={Uri.EscapeDataString(RequiredScope)}" +
        "&access_type=offline" +
        "&prompt=consent" +
        "&include_granted_scopes=false";

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

        var calendarId = ResolvedCalendarId;
        var encodedCalendarId = Uri.EscapeDataString(calendarId);
        var timeZoneId = CalendarTime.ToGoogleTimeZoneId(_options.TimeZone);
        var tz = CalendarTime.Resolve(timeZoneId);
        var startLocal = CalendarTime.ToWallClockRfc3339(request.Start, tz);
        var endLocal = CalendarTime.ToWallClockRfc3339(request.End, tz);
        var payload = new Dictionary<string, object?>
        {
            ["summary"] = request.Summary,
            ["description"] = request.Description,
            ["start"] = new Dictionary<string, string>
            {
                ["dateTime"] = startLocal,
                ["timeZone"] = timeZoneId
            },
            ["end"] = new Dictionary<string, string>
            {
                ["dateTime"] = endLocal,
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

        var eventId = request.ExistingEventId;
        if (string.IsNullOrWhiteSpace(eventId))
        {
            eventId = await FindExistingEventIdAsync(access.Token!, request.AppointmentId, encodedCalendarId, cancellationToken);
        }

        var postUrl = $"https://www.googleapis.com/calendar/v3/calendars/{encodedCalendarId}/events";
        var method = string.IsNullOrWhiteSpace(eventId) ? HttpMethod.Post : HttpMethod.Put;
        var url = string.IsNullOrWhiteSpace(eventId)
            ? postUrl
            : $"{postUrl}/{Uri.EscapeDataString(eventId)}";

        var write = await SendEventWriteAsync(method, url, access.Token!, payload, cancellationToken);
        if (!write.Success && method == HttpMethod.Put && write.StatusCode == (int)HttpStatusCode.NotFound)
        {
            _logger.LogWarning(
                "Google Calendar PUT HTTP 404 calendarId={CalendarId} appointmentId={AppointmentId} eventId={EventId}. Creating a new event.",
                calendarId,
                request.AppointmentId,
                eventId);
            write = await SendEventWriteAsync(HttpMethod.Post, postUrl, access.Token!, payload, cancellationToken);
            method = HttpMethod.Post;
        }

        if (!write.Success)
        {
            _logger.LogWarning(
                "Google Calendar {Method} failed: status={Status} calendarId={CalendarId} appointmentId={AppointmentId} eventId={EventId} body={Body}",
                method.Method,
                write.StatusCode,
                calendarId,
                request.AppointmentId,
                eventId,
                SanitizeDiagnosticText(Truncate(write.Body)));
            return CalendarSyncResult.Fail(
                ProviderName,
                $"Google Calendar HTTP {write.StatusCode}",
                googleStatusCode: write.StatusCode,
                calendarId: calendarId);
        }

        var createdId = write.EventId ?? eventId;
        _logger.LogInformation(
            "Google Calendar {Method} succeeded: status={Status} calendarId={CalendarId} appointmentId={AppointmentId} eventId={EventId} timeZone={TimeZone} start={Start} end={End} body={Body}",
            method.Method,
            write.StatusCode,
            calendarId,
            request.AppointmentId,
            createdId,
            timeZoneId,
            startLocal,
            endLocal,
            SanitizeDiagnosticText(Truncate(write.Body)));

        if (string.IsNullOrWhiteSpace(createdId))
        {
            return CalendarSyncResult.Fail(
                ProviderName,
                "Google Calendar returned success without an event id.",
                googleStatusCode: write.StatusCode,
                calendarId: calendarId);
        }

        var verify = await FetchEventAsync(access.Token!, calendarId, createdId, cancellationToken);
        if (!verify.Found)
        {
            _logger.LogWarning(
                "Google Calendar event {EventId} was not retrievable after write (HTTP {Status}) calendarId={CalendarId} appointmentId={AppointmentId}",
                createdId,
                verify.GoogleStatusCode,
                calendarId,
                request.AppointmentId);
            return CalendarSyncResult.Fail(
                ProviderName,
                $"Google Calendar wrote event {createdId} but GET returned HTTP {verify.GoogleStatusCode ?? 0}.",
                googleStatusCode: verify.GoogleStatusCode,
                calendarId: calendarId);
        }

        return CalendarSyncResult.Ok(
            ProviderName,
            createdId,
            htmlLink: verify.HtmlLink ?? write.HtmlLink,
            timeZone: verify.TimeZone ?? timeZoneId,
            calendarId: calendarId,
            googleStatusCode: verify.GoogleStatusCode ?? write.StatusCode);
    }

    public async Task<GoogleCalendarEventDebugResult> GetEventDebugAsync(
        string eventId,
        CancellationToken cancellationToken = default)
    {
        var calendarId = ResolvedCalendarId;
        var result = new GoogleCalendarEventDebugResult
        {
            Provider = ProviderName,
            CalendarId = calendarId,
            EventId = eventId
        };

        if (string.IsNullOrWhiteSpace(eventId))
        {
            result.Error = "eventId is required.";
            return result;
        }

        try
        {
            var access = await GetAccessTokenAsync(cancellationToken);
            if (access.Error != null)
            {
                result.Error = access.Error;
                return result;
            }

            return await FetchEventAsync(access.Token!, calendarId, eventId, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Google Calendar debug-event lookup failed");
            result.Error = SanitizeDiagnosticText($"Google Calendar debug lookup failed: {ex.Message}");
            return result;
        }
    }

    public async Task<GoogleCalendarAccountDebugResult> GetAccountDebugAsync(CancellationToken cancellationToken = default)
    {
        var calendarId = ResolvedCalendarId;
        var result = new GoogleCalendarAccountDebugResult
        {
            Provider = ProviderName,
            CalendarId = calendarId,
            TimeZone = CalendarTime.ToGoogleTimeZoneId(_options.TimeZone),
            OauthClientConfigured = IsConfigured,
            HasStoredGrant = HasStoredGrant,
            RequiredScope = RequiredScope
        };

        AttachStoredScope(result);

        try
        {
            var access = await GetAccessTokenAsync(cancellationToken);
            if (access.Error != null)
            {
                result.Error = access.Error;
                result.ReconnectRequired = true;
                result.ReconnectReason ??= "Google Calendar is not connected. Visit /api/integrations/google/authorize.";
                return result;
            }

            var liveScope = await FetchLiveScopeAsync(access.Token!, cancellationToken);
            result.LiveScope = liveScope;
            var liveCoversEvents = ScopeCoversCalendarEvents(liveScope);

            using var message = new HttpRequestMessage(
                HttpMethod.Get,
                $"https://www.googleapis.com/calendar/v3/calendars/{Uri.EscapeDataString(calendarId)}/events?maxResults=1&singleEvents=true");
            message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", access.Token);
            using var response = await _http.SendAsync(message, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            result.GoogleStatusCode = (int)response.StatusCode;

            if (response.IsSuccessStatusCode)
            {
                result.ScopeSufficient = true;
                using var doc = JsonDocument.Parse(body);
                if (doc.RootElement.TryGetProperty("items", out var items) && items.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in items.EnumerateArray())
                    {
                        if (item.TryGetProperty("organizer", out var organizer) && organizer.ValueKind == JsonValueKind.Object)
                        {
                            result.Email ??= GetJsonString(organizer, "email");
                            result.CalendarSummary ??= GetJsonString(organizer, "displayName");
                        }

                        if (item.TryGetProperty("creator", out var creator) && creator.ValueKind == JsonValueKind.Object)
                        {
                            result.Email ??= GetJsonString(creator, "email");
                        }

                        if (item.TryGetProperty("start", out var startEl) && startEl.ValueKind == JsonValueKind.Object)
                        {
                            result.TimeZone = GetJsonString(startEl, "timeZone") ?? result.TimeZone;
                        }

                        if (!string.IsNullOrWhiteSpace(result.Email))
                        {
                            break;
                        }
                    }
                }

                result.ResolvedCalendarId = result.Email ?? calendarId;
                result.Email = EmailIfLooksLikeAddress(result.Email) ?? EmailIfLooksLikeAddress(result.ResolvedCalendarId);
                return result;
            }

            result.Error = DescribeGoogleApiFailure((int)response.StatusCode, body);
            result.ScopeSufficient = false;
            result.ReconnectRequired = true;
            result.ReconnectReason =
                liveCoversEvents
                    ? result.Error
                    : "Live access token does not include https://www.googleapis.com/auth/calendar.events. Visit /api/integrations/google/authorize to force a new consent grant. The database Scope column is not proof of the token's permissions.";
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Google Calendar debug-account lookup failed");
            result.Error = SanitizeDiagnosticText($"Google Calendar account lookup failed: {ex.Message}");
            return result;
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

        var calendarId = Uri.EscapeDataString(ResolvedCalendarId);
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
            _logger.LogWarning(
                "Google Calendar delete failed: {Status} {Body}",
                (int)response.StatusCode,
                SanitizeDiagnosticText(Truncate(body)));
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
            var reason = DescribeOAuthRefreshFailure((int)response.StatusCode, body);
            _logger.LogWarning("Google OAuth refresh failed: {Reason}", reason);
            return (null, reason);
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
        if (doc.RootElement.TryGetProperty("scope", out var refreshScope) &&
            refreshScope.ValueKind == JsonValueKind.String &&
            !string.IsNullOrWhiteSpace(refreshScope.GetString()))
        {
            token.Scope = refreshScope.GetString();
        }

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

    public Task StoreAuthorizationCodeAsync(string code, CancellationToken cancellationToken = default) =>
        StoreAuthorizationCodeAsync(code, EffectiveRedirectUri(), cancellationToken);

    public async Task StoreAuthorizationCodeAsync(string code, string redirectUri, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://oauth2.googleapis.com/token");
        request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["code"] = code,
            ["client_id"] = _options.ClientId,
            ["client_secret"] = _options.ClientSecret,
            ["redirect_uri"] = string.IsNullOrWhiteSpace(redirectUri) ? EffectiveRedirectUri() : redirectUri,
            ["grant_type"] = "authorization_code"
        });

        using var response = await _http.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "Google OAuth code exchange failed with HTTP {Status}. {Detail}",
                (int)response.StatusCode,
                SanitizeDiagnosticText(body));
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
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            throw new InvalidOperationException(
                "Google OAuth did not return a refresh token. Revisit /api/integrations/google/authorize so prompt=consent can issue a new grant. The previous refresh token was not reused.");
        }

        var existing = await db.IntegrationTokens.FirstOrDefaultAsync(x => x.Provider == ProviderKey, cancellationToken);
        if (existing == null)
        {
            existing = new IntegrationToken { Id = Guid.NewGuid(), Provider = ProviderKey };
            db.IntegrationTokens.Add(existing);
        }

        existing.AccessToken = accessToken;
        existing.RefreshToken = refreshToken;

        existing.AccessTokenExpiresAt = DateTimeOffset.UtcNow.AddSeconds(expiresIn);
        existing.Scope = scope;
        existing.TokenType = tokenType;
        existing.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }

    private string ResolvedCalendarId =>
        string.IsNullOrWhiteSpace(_options.CalendarId) ? "primary" : _options.CalendarId.Trim();

    private async Task<(bool Success, int StatusCode, string Body, string? EventId, string? HtmlLink)> SendEventWriteAsync(
        HttpMethod method,
        string url,
        string accessToken,
        Dictionary<string, object?> payload,
        CancellationToken cancellationToken)
    {
        using var message = new HttpRequestMessage(method, url);
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        message.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        using var response = await _http.SendAsync(message, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        string? id = null;
        string? htmlLink = null;
        if (response.IsSuccessStatusCode)
        {
            try
            {
                using var doc = JsonDocument.Parse(body);
                id = GetJsonString(doc.RootElement, "id");
                htmlLink = GetJsonString(doc.RootElement, "htmlLink");
            }
            catch (JsonException)
            {
            }
        }

        return (response.IsSuccessStatusCode, (int)response.StatusCode, body, id, htmlLink);
    }

    private async Task<GoogleCalendarEventDebugResult> FetchEventAsync(
        string accessToken,
        string calendarId,
        string eventId,
        CancellationToken cancellationToken)
    {
        var result = new GoogleCalendarEventDebugResult
        {
            Provider = ProviderName,
            CalendarId = calendarId,
            EventId = eventId
        };

        using var message = new HttpRequestMessage(
            HttpMethod.Get,
            $"https://www.googleapis.com/calendar/v3/calendars/{Uri.EscapeDataString(calendarId)}/events/{Uri.EscapeDataString(eventId)}");
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await _http.SendAsync(message, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        result.GoogleStatusCode = (int)response.StatusCode;

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            result.Found = false;
            return result;
        }

        if (!response.IsSuccessStatusCode)
        {
            result.Found = false;
            result.Error = DescribeGoogleApiFailure((int)response.StatusCode, body);
            return result;
        }

        using var doc = JsonDocument.Parse(body);
        PopulateEventDebug(result, doc.RootElement);
        return result;
    }

    private static void PopulateEventDebug(GoogleCalendarEventDebugResult result, JsonElement root)
    {
        result.Found = true;
        result.Summary = GetJsonString(root, "summary");
        result.Description = GetJsonString(root, "description");
        result.HtmlLink = GetJsonString(root, "htmlLink");
        result.Status = GetJsonString(root, "status");
        if (root.TryGetProperty("organizer", out var organizer) && organizer.ValueKind == JsonValueKind.Object)
        {
            result.OrganizerEmail = GetJsonString(organizer, "email");
        }

        if (root.TryGetProperty("creator", out var creator) && creator.ValueKind == JsonValueKind.Object)
        {
            result.CreatorEmail = GetJsonString(creator, "email");
        }

        if (root.TryGetProperty("start", out var startEl) && startEl.ValueKind == JsonValueKind.Object)
        {
            result.Start = GetJsonString(startEl, "dateTime") ?? GetJsonString(startEl, "date");
            result.TimeZone = GetJsonString(startEl, "timeZone");
        }

        if (root.TryGetProperty("end", out var endEl) && endEl.ValueKind == JsonValueKind.Object)
        {
            result.End = GetJsonString(endEl, "dateTime") ?? GetJsonString(endEl, "date");
            result.TimeZone ??= GetJsonString(endEl, "timeZone");
        }
    }

    private void AttachStoredScope(GoogleCalendarAccountDebugResult result)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ReignDbContext>();
        var token = db.IntegrationTokens.AsNoTracking().FirstOrDefault(x => x.Provider == ProviderKey);
        result.StoredScope = token?.Scope;
    }

    private async Task<string?> FetchLiveScopeAsync(string accessToken, CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "https://oauth2.googleapis.com/tokeninfo");
            request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["access_token"] = accessToken
            });
            using var response = await _http.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            return GetJsonString(doc.RootElement, "scope");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Google tokeninfo scope lookup failed");
            return null;
        }
    }

    internal static bool ScopeCoversCalendarEvents(string? scope)
    {
        if (string.IsNullOrWhiteSpace(scope))
        {
            return false;
        }

        var parts = scope.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Any(part =>
            part.Equals(RequiredScope, StringComparison.OrdinalIgnoreCase) ||
            part.Equals("https://www.googleapis.com/auth/calendar", StringComparison.OrdinalIgnoreCase));
    }

    private static string? EmailIfLooksLikeAddress(string? value) =>
        !string.IsNullOrWhiteSpace(value) && value.Contains('@', StringComparison.Ordinal)
            ? value
            : null;

    private static string Truncate(string value) =>
        value.Length <= 300 ? value : value[..300];

    private static string DescribeOAuthRefreshFailure(int statusCode, string body)
    {
        var reason = $"Google OAuth refresh failed (HTTP {statusCode})";
        try
        {
            using var doc = JsonDocument.Parse(body);
            var error = GetJsonString(doc.RootElement, "error");
            var description = GetJsonString(doc.RootElement, "error_description");
            var details = new List<string>();
            if (!string.IsNullOrWhiteSpace(error))
            {
                details.Add(error);
            }

            if (!string.IsNullOrWhiteSpace(description))
            {
                details.Add(description);
            }

            if (details.Count > 0)
            {
                reason = $"{reason}: {string.Join(" — ", details)}";
            }
        }
        catch (JsonException)
        {
            reason = $"{reason}. Response was not JSON.";
        }

        return SanitizeDiagnosticText(reason);
    }

    private static string DescribeGoogleApiFailure(int statusCode, string body)
    {
        var reason = $"Google Calendar HTTP {statusCode}";
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (!doc.RootElement.TryGetProperty("error", out var errorEl))
            {
                return SanitizeDiagnosticText($"{reason}.");
            }

            if (errorEl.ValueKind == JsonValueKind.String)
            {
                var error = errorEl.GetString();
                if (!string.IsNullOrWhiteSpace(error))
                {
                    reason = $"{reason}: {error}";
                }
            }
            else if (errorEl.ValueKind == JsonValueKind.Object)
            {
                var status = GetJsonString(errorEl, "status");
                var message = GetJsonString(errorEl, "message");
                var details = new List<string>();
                if (!string.IsNullOrWhiteSpace(status))
                {
                    details.Add(status);
                }

                if (!string.IsNullOrWhiteSpace(message))
                {
                    details.Add(message);
                }

                if (details.Count > 0)
                {
                    reason = $"{reason}: {string.Join(" — ", details)}";
                }
            }
        }
        catch (JsonException)
        {
            reason = $"{reason}.";
        }

        return SanitizeDiagnosticText(reason);
    }

    private static string? GetJsonString(JsonElement parent, string name)
    {
        if (!parent.TryGetProperty(name, out var el) || el.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        return el.GetString();
    }

    private string EffectiveRedirectUri()
    {
        var isDevelopment = string.Equals(
            Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
                ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT"),
            "Development",
            StringComparison.OrdinalIgnoreCase);
        if (isDevelopment && GoogleRedirectUri.RunningInContainer())
        {
            return GoogleRedirectUri.DockerCallback;
        }

        return _options.RedirectUri;
    }

    private static readonly Regex SecretJsonProperty = new(
        """"(access_token|refresh_token|id_token|client_secret|authorization_code)"\s*:\s*"(?:\\.|[^"\\])*"""",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex SecretFormField = new(
        @"(?:^|[?&\s])(access_token|refresh_token|client_secret|authorization_code)=([^\s&]+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex BearerToken = new(
        @"Bearer\s+[A-Za-z0-9._\-=~+/]+",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    internal static string SanitizeDiagnosticText(string value)
    {
        var sanitized = SecretJsonProperty.Replace(value, "\"$1\":\"[redacted]\"");
        sanitized = BearerToken.Replace(sanitized, "Bearer [redacted]");
        sanitized = SecretFormField.Replace(sanitized, "$1=[redacted]");
        return sanitized;
    }
}
