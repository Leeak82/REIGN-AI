using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using REIGN.API.Calendar;
using REIGN.API.Options;
using REIGN.Data;
using REIGN.Data.Models;
using REIGN.Data.Schema;
using Xunit;

namespace REIGN.Tests;

public class GoogleCalendarDebugTests
{
    private static readonly JsonSerializerOptions CamelCase = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    [Fact]
    public async Task Debug_event_returns_google_fields_without_tokens()
    {
        const string accessToken = "SECRET_ACCESS_TOKEN_VALUE_XYZ";
        const string refreshToken = "SECRET_REFRESH_TOKEN_VALUE_XYZ";
        const string clientSecret = "SECRET_CLIENT_SECRET_XYZ";
        const string eventId = "1tfftt6crrdcaju5iimch6r3lc";

        await using var harness = await Harness.CreateAsync(
            calendarId: "",
            clientSecret: clientSecret,
            accessToken: accessToken,
            refreshToken: refreshToken,
            accessExpiresAt: DateTimeOffset.UtcNow.AddHours(1));

        harness.Handler.Respond = request =>
        {
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.Equal(
                $"https://www.googleapis.com/calendar/v3/calendars/primary/events/{eventId}",
                request.RequestUri?.ToString());
            Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
            Assert.Equal(accessToken, request.Headers.Authorization?.Parameter);
            return JsonResponse(HttpStatusCode.OK, """
                {
                  "id": "1tfftt6crrdcaju5iimch6r3lc",
                  "summary": "Oil change",
                  "description": "Confirmed appointment",
                  "htmlLink": "https://www.google.com/calendar/event?eid=abc",
                  "start": { "dateTime": "2026-08-20T10:00:00-04:00", "timeZone": "America/New_York" },
                  "end": { "dateTime": "2026-08-20T10:30:00-04:00", "timeZone": "America/New_York" }
                }
                """);
        };

        var result = await harness.Service.GetEventDebugAsync(eventId);

        Assert.Equal("Google", result.Provider);
        Assert.Equal("primary", result.CalendarId);
        Assert.Equal(eventId, result.EventId);
        Assert.True(result.Found);
        Assert.Equal(200, result.GoogleStatusCode);
        Assert.Equal("Oil change", result.Summary);
        Assert.Equal("Confirmed appointment", result.Description);
        Assert.Equal("2026-08-20T10:00:00-04:00", result.Start);
        Assert.Equal("2026-08-20T10:30:00-04:00", result.End);
        Assert.Equal("America/New_York", result.TimeZone);
        Assert.Equal("https://www.google.com/calendar/event?eid=abc", result.HtmlLink);
        Assert.Null(result.Error);
        Assert.DoesNotContain(harness.Handler.Urls, url => url.Contains("oauth2.googleapis.com", StringComparison.Ordinal));
        AssertNoSecrets(result, accessToken, refreshToken, clientSecret);
    }

    [Fact]
    public async Task Debug_event_uses_configured_calendar_id()
    {
        await using var harness = await Harness.CreateAsync(
            calendarId: "studio@example.com",
            accessExpiresAt: DateTimeOffset.UtcNow.AddHours(1));

        harness.Handler.Respond = request =>
        {
            Assert.Equal(
                "https://www.googleapis.com/calendar/v3/calendars/studio%40example.com/events/evt-1",
                request.RequestUri?.ToString());
            return JsonResponse(HttpStatusCode.OK, """{ "summary": "Cut" }""");
        };

        var result = await harness.Service.GetEventDebugAsync("evt-1");
        Assert.Equal("studio@example.com", result.CalendarId);
        Assert.True(result.Found);
        Assert.Equal("Cut", result.Summary);
    }

    [Fact]
    public async Task Debug_event_returns_found_false_on_google_404()
    {
        await using var harness = await Harness.CreateAsync(accessExpiresAt: DateTimeOffset.UtcNow.AddHours(1));
        harness.Handler.Respond = _ => JsonResponse(HttpStatusCode.NotFound, """
            { "error": { "code": 404, "message": "Not Found", "status": "NOT_FOUND" } }
            """);

        var result = await harness.Service.GetEventDebugAsync("missing-event");

        Assert.False(result.Found);
        Assert.Equal(404, result.GoogleStatusCode);
        Assert.Equal("primary", result.CalendarId);
        Assert.Null(result.Error);
        Assert.Null(result.Summary);
    }

    [Fact]
    public async Task Debug_event_returns_oauth_refresh_reason_without_secrets()
    {
        const string accessToken = "EXPIRED_ACCESS_TOKEN_XYZ";
        const string refreshToken = "SECRET_REFRESH_TOKEN_VALUE_XYZ";
        const string clientSecret = "SECRET_CLIENT_SECRET_XYZ";
        const string leaked = "SHOULD_NOT_LEAK_ACCESS_TOKEN";

        await using var harness = await Harness.CreateAsync(
            clientSecret: clientSecret,
            accessToken: accessToken,
            refreshToken: refreshToken,
            accessExpiresAt: DateTimeOffset.UtcNow.AddMinutes(-1));

        harness.Handler.Respond = request =>
        {
            Assert.Equal("https://oauth2.googleapis.com/token", request.RequestUri?.ToString());
            return JsonResponse(HttpStatusCode.BadRequest, $$"""
                {
                  "error": "invalid_grant",
                  "error_description": "Token has been expired or revoked. Bearer {{leaked}}",
                  "access_token": "{{leaked}}"
                }
                """);
        };

        var result = await harness.Service.GetEventDebugAsync("evt-1");

        Assert.False(result.Found);
        Assert.Null(result.GoogleStatusCode);
        Assert.Contains("invalid_grant", result.Error, StringComparison.Ordinal);
        Assert.Contains("HTTP 400", result.Error, StringComparison.Ordinal);
        Assert.DoesNotContain(leaked, result.Error, StringComparison.Ordinal);
        Assert.DoesNotContain(refreshToken, result.Error, StringComparison.Ordinal);
        Assert.DoesNotContain(clientSecret, result.Error, StringComparison.Ordinal);
        AssertNoSecrets(result, accessToken, refreshToken, clientSecret, leaked);
    }

    [Fact]
    public async Task Debug_event_reports_missing_grant_without_calling_google()
    {
        await using var harness = await Harness.CreateAsync(storeGrant: false);
        var result = await harness.Service.GetEventDebugAsync("evt-1");

        Assert.False(result.Found);
        Assert.Null(result.GoogleStatusCode);
        Assert.Contains("not connected", result.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(harness.Handler.Urls);
    }

    [Fact]
    public void Sanitize_diagnostic_text_redacts_tokens_and_keeps_reason()
    {
        var sanitized = GoogleCalendarService.SanitizeDiagnosticText(
            """Google OAuth refresh failed (HTTP 400): invalid_grant — Bearer SECRET_VALUE access_token="SECRET_VALUE" refresh_token=SECRET_VALUE""");

        Assert.Contains("invalid_grant", sanitized, StringComparison.Ordinal);
        Assert.Contains("HTTP 400", sanitized, StringComparison.Ordinal);
        Assert.DoesNotContain("SECRET_VALUE", sanitized, StringComparison.Ordinal);
        Assert.Contains("[redacted]", sanitized, StringComparison.Ordinal);
    }

    private static void AssertNoSecrets(
        GoogleCalendarEventDebugResult result,
        params string[] secrets)
    {
        var json = JsonSerializer.Serialize(result, CamelCase);
        Assert.Contains("\"provider\":\"Google\"", json, StringComparison.Ordinal);
        foreach (var secret in secrets)
        {
            Assert.DoesNotContain(secret, json, StringComparison.Ordinal);
        }
    }

    private static HttpResponseMessage JsonResponse(HttpStatusCode status, string json) =>
        new(status)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

    private sealed class ScriptedHandler : HttpMessageHandler
    {
        public List<string> Urls { get; } = [];

        public Func<HttpRequestMessage, HttpResponseMessage> Respond { get; set; } =
            _ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{}", Encoding.UTF8, "application/json")
            };

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Urls.Add(request.RequestUri?.ToString() ?? "");
            return Task.FromResult(Respond(request));
        }
    }

    private sealed class Harness : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly ReignDbContext _db;

        public ScriptedHandler Handler { get; }
        public GoogleCalendarService Service { get; }

        private Harness(
            SqliteConnection connection,
            ReignDbContext db,
            ScriptedHandler handler,
            GoogleCalendarService service)
        {
            _connection = connection;
            _db = db;
            Handler = handler;
            Service = service;
        }

        public static async Task<Harness> CreateAsync(
            string calendarId = "primary",
            string clientId = "client-id",
            string clientSecret = "client-secret",
            string accessToken = "access-token",
            string refreshToken = "refresh-token",
            DateTimeOffset? accessExpiresAt = null,
            bool storeGrant = true)
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();

            var options = new DbContextOptionsBuilder<ReignDbContext>()
                .UseSqlite(connection)
                .Options;
            var db = new ReignDbContext(options);
            await SqliteSchemaUpgrades.ApplyAsync(db);

            if (storeGrant)
            {
                db.IntegrationTokens.Add(new IntegrationToken
                {
                    Provider = GoogleCalendarService.ProviderKey,
                    AccessToken = accessToken,
                    RefreshToken = refreshToken,
                    AccessTokenExpiresAt = accessExpiresAt ?? DateTimeOffset.UtcNow.AddHours(1),
                    TokenType = "Bearer"
                });
                await db.SaveChangesAsync();
            }

            var handler = new ScriptedHandler();
            var service = new GoogleCalendarService(
                new HttpClient(handler),
                Options.Create(new GoogleCalendarOptions
                {
                    Provider = "Google",
                    ClientId = clientId,
                    ClientSecret = clientSecret,
                    CalendarId = calendarId
                }),
                new DbScopeFactory(db),
                NullLogger<GoogleCalendarService>.Instance);

            return new Harness(connection, db, handler, service);
        }

        public async ValueTask DisposeAsync()
        {
            await _db.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }

    private sealed class DbScopeFactory : IServiceScopeFactory, IServiceScope, IServiceProvider
    {
        private readonly ReignDbContext _db;

        public DbScopeFactory(ReignDbContext db) => _db = db;

        public IServiceScope CreateScope() => this;

        public IServiceProvider ServiceProvider => this;

        public object? GetService(Type serviceType) =>
            serviceType == typeof(ReignDbContext) ? _db : null;

        public void Dispose()
        {
        }
    }
}
