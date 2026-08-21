using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using REIGN.API.Calendar;
using REIGN.API.Configuration;
using REIGN.API.Controllers;
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

    [Fact]
    public async Task Debug_event_endpoint_is_not_found_outside_development()
    {
        await using var harness = await Harness.CreateAsync();
        var controller = CreateController(harness.Service, Environments.Production);
        var result = await controller.GoogleDebugEvent("evt-1", CancellationToken.None);
        Assert.IsType<NotFoundResult>(result);
        Assert.Empty(harness.Handler.Urls);
    }

    [Fact]
    public async Task Debug_event_endpoint_returns_diagnostic_payload_in_development()
    {
        await using var harness = await Harness.CreateAsync(accessExpiresAt: DateTimeOffset.UtcNow.AddHours(1));
        harness.Handler.Respond = _ => JsonResponse(HttpStatusCode.OK, """{ "summary": "Cut" }""");
        var controller = CreateController(harness.Service, Environments.Development);
        var result = await controller.GoogleDebugEvent("1tfftt6crrdcaju5iimch6r3lc", CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(result);
        var body = Assert.IsType<GoogleCalendarEventDebugResult>(ok.Value);
        Assert.True(body.Found);
        Assert.Equal(200, body.GoogleStatusCode);
        Assert.Equal("Cut", body.Summary);
        Assert.Equal("1tfftt6crrdcaju5iimch6r3lc", body.EventId);
    }

    [Fact]
    public void Authorization_url_forces_offline_consent_and_calendar_events_scope()
    {
        using var env = new EnvScope();
        env.ClearDockerRuntimeMarkers();
        env.Set("GoogleCalendar__RedirectUri", null);
        env.Set("GOOGLE_REDIRECT_URI", null);

        var url = GoogleCalendarService.BuildAuthorizationUrl(
            "test-client-id",
            "https://localhost:5001/api/integrations/google/callback");

        Assert.StartsWith("https://accounts.google.com/o/oauth2/v2/auth?", url, StringComparison.Ordinal);
        Assert.Contains("access_type=offline", url, StringComparison.Ordinal);
        Assert.Contains("prompt=consent", url, StringComparison.Ordinal);
        Assert.Contains("include_granted_scopes=false", url, StringComparison.Ordinal);
        Assert.Contains(Uri.EscapeDataString(GoogleCalendarService.RequiredScope), url, StringComparison.Ordinal);
        Assert.Contains("response_type=code", url, StringComparison.Ordinal);
        Assert.Contains(Uri.EscapeDataString("https://localhost:5001/api/integrations/google/callback"), url, StringComparison.Ordinal);
    }

    [Fact]
    public void Authorization_url_rewrites_5001_when_REIGN_DOCKER_is_set()
    {
        using var env = new EnvScope();
        env.ClearDockerRuntimeMarkers();
        env.Set("REIGN_DOCKER", "1");
        env.Set("GoogleCalendar__RedirectUri", GoogleRedirectUri.KestrelHttpsCallback);
        env.Set("GOOGLE_REDIRECT_URI", GoogleRedirectUri.KestrelHttpsCallback);

        var url = GoogleCalendarService.BuildAuthorizationUrl(
            "test-client-id",
            GoogleRedirectUri.KestrelHttpsCallback);

        Assert.Contains(Uri.EscapeDataString(GoogleRedirectUri.DockerCallback), url, StringComparison.Ordinal);
        Assert.DoesNotContain("localhost:5001", url, StringComparison.Ordinal);
    }

    [Fact]
    public void Authorize_endpoint_redirects_to_forced_consent_url()
    {
        var google = Options.Create(new GoogleCalendarOptions
        {
            ClientId = "local-client-id",
            ClientSecret = "local-client-secret",
            RedirectUri = "https://localhost:5001/api/integrations/google/callback"
        });
        var controller = new IntegrationsController(
            sms: null!,
            calendar: null!,
            googleCalendar: null!,
            google: google,
            smsOptions: Options.Create(new SmsOptions()),
            environment: new StubHostEnvironment { EnvironmentName = Environments.Development },
            logger: NullLogger<IntegrationsController>.Instance);

        var result = Assert.IsType<RedirectResult>(controller.GoogleAuthorize());
        Assert.Contains("prompt=consent", result.Url, StringComparison.Ordinal);
        Assert.Contains("access_type=offline", result.Url, StringComparison.Ordinal);
        Assert.Contains(Uri.EscapeDataString(GoogleCalendarService.RequiredScope), result.Url, StringComparison.Ordinal);
        Assert.Contains("include_granted_scopes=false", result.Url, StringComparison.Ordinal);
    }

    [Fact]
    public void Authorize_endpoint_uses_docker_localhost_8080_redirect_when_configured()
    {
        using var env = new EnvScope();
        env.ClearDockerRuntimeMarkers();
        env.Set("GoogleCalendar__RedirectUri", GoogleRedirectUri.KestrelHttpsCallback);
        env.Set("GOOGLE_REDIRECT_URI", GoogleRedirectUri.KestrelHttpsCallback);

        const string dockerCallback = "http://localhost:8080/api/integrations/google/callback";
        var google = Options.Create(new GoogleCalendarOptions
        {
            ClientId = "docker-client-id",
            ClientSecret = "docker-client-secret",
            RedirectUri = dockerCallback
        });
        var controller = new IntegrationsController(
            sms: null!,
            calendar: null!,
            googleCalendar: null!,
            google: google,
            smsOptions: Options.Create(new SmsOptions()),
            environment: new StubHostEnvironment { EnvironmentName = Environments.Development },
            logger: NullLogger<IntegrationsController>.Instance);

        var result = Assert.IsType<RedirectResult>(controller.GoogleAuthorize());
        Assert.Contains(Uri.EscapeDataString(dockerCallback), result.Url, StringComparison.Ordinal);
        Assert.DoesNotContain("localhost:5001", result.Url, StringComparison.Ordinal);
        Assert.Contains("access_type=offline", result.Url, StringComparison.Ordinal);
        Assert.Contains("prompt=consent", result.Url, StringComparison.Ordinal);
    }

    [Fact]
    public void Authorize_endpoint_rewrites_5001_when_request_host_is_localhost_8080()
    {
        using var env = new EnvScope();
        env.ClearDockerRuntimeMarkers();

        var google = Options.Create(new GoogleCalendarOptions
        {
            ClientId = "docker-client-id",
            ClientSecret = "docker-client-secret",
            RedirectUri = "https://localhost:5001/api/integrations/google/callback"
        });
        var http = new DefaultHttpContext();
        http.Request.Scheme = "http";
        http.Request.Host = new HostString("localhost", 8080);
        http.Request.Path = "/api/integrations/google/authorize";
        var controller = new IntegrationsController(
            sms: null!,
            calendar: null!,
            googleCalendar: null!,
            google: google,
            smsOptions: Options.Create(new SmsOptions()),
            environment: new StubHostEnvironment { EnvironmentName = Environments.Development },
            logger: NullLogger<IntegrationsController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = http }
        };

        var result = Assert.IsType<RedirectResult>(controller.GoogleAuthorize());
        Assert.Contains(
            Uri.EscapeDataString("http://localhost:8080/api/integrations/google/callback"),
            result.Url,
            StringComparison.Ordinal);
        Assert.DoesNotContain("localhost:5001", result.Url, StringComparison.Ordinal);
    }

    [Fact]
    public void Authorize_endpoint_keeps_kestrel_5001_when_request_host_is_localhost_5001()
    {
        using var env = new EnvScope();
        env.ClearDockerRuntimeMarkers();
        env.Set("GoogleCalendar__RedirectUri", null);
        env.Set("GOOGLE_REDIRECT_URI", null);

        const string kestrelCallback = "https://localhost:5001/api/integrations/google/callback";
        var google = Options.Create(new GoogleCalendarOptions
        {
            ClientId = "kestrel-client-id",
            ClientSecret = "kestrel-client-secret",
            RedirectUri = kestrelCallback
        });
        var http = new DefaultHttpContext();
        http.Request.Scheme = "https";
        http.Request.Host = new HostString("localhost", 5001);
        http.Request.Path = "/api/integrations/google/authorize";
        var controller = new IntegrationsController(
            sms: null!,
            calendar: null!,
            googleCalendar: null!,
            google: google,
            smsOptions: Options.Create(new SmsOptions()),
            environment: new StubHostEnvironment { EnvironmentName = Environments.Development },
            logger: NullLogger<IntegrationsController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = http }
        };

        var result = Assert.IsType<RedirectResult>(controller.GoogleAuthorize());
        Assert.Contains(Uri.EscapeDataString(kestrelCallback), result.Url, StringComparison.Ordinal);
        Assert.DoesNotContain("localhost:8080", result.Url, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Debug_account_returns_primary_calendar_email_without_tokens()
    {
        const string accessToken = "SECRET_ACCESS_TOKEN_VALUE_XYZ";
        await using var harness = await Harness.CreateAsync(
            accessToken: accessToken,
            accessExpiresAt: DateTimeOffset.UtcNow.AddHours(1));

        harness.Handler.Respond = request =>
        {
            var url = request.RequestUri!.ToString();
            if (url.Contains("oauth2.googleapis.com/tokeninfo", StringComparison.Ordinal))
            {
                return JsonResponse(HttpStatusCode.OK, """{ "scope": "https://www.googleapis.com/auth/calendar.events" }""");
            }

            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.Contains("/calendars/primary/events", url, StringComparison.Ordinal);
            return JsonResponse(HttpStatusCode.OK, """
                {
                  "items": [
                    {
                      "organizer": { "email": "studio@example.com", "displayName": "Studio" },
                      "start": { "timeZone": "America/New_York" }
                    }
                  ]
                }
                """);
        };

        var result = await harness.Service.GetAccountDebugAsync();
        Assert.Equal("Google", result.Provider);
        Assert.Equal("primary", result.CalendarId);
        Assert.Equal("studio@example.com", result.ResolvedCalendarId);
        Assert.Equal("studio@example.com", result.Email);
        Assert.Equal("Studio", result.CalendarSummary);
        Assert.Equal("America/New_York", result.TimeZone);
        Assert.Equal(GoogleCalendarService.RequiredScope, result.RequiredScope);
        Assert.Equal(GoogleCalendarService.RequiredScope, result.LiveScope);
        Assert.True(result.ScopeSufficient);
        Assert.False(result.ReconnectRequired);
        Assert.Equal(200, result.GoogleStatusCode);
        var json = JsonSerializer.Serialize(result, CamelCase);
        Assert.DoesNotContain(accessToken, json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Debug_account_endpoint_is_not_found_outside_development()
    {
        await using var harness = await Harness.CreateAsync();
        var controller = CreateController(harness.Service, Environments.Production);
        var result = await controller.GoogleDebugAccount(CancellationToken.None);
        Assert.IsType<NotFoundResult>(result);
        Assert.Empty(harness.Handler.Urls);
    }

    [Fact]
    public async Task Database_scope_column_is_not_treated_as_live_permission()
    {
        await using var harness = await Harness.CreateAsync(
            accessExpiresAt: DateTimeOffset.UtcNow.AddHours(1),
            scope: GoogleCalendarService.RequiredScope);

        harness.Handler.Respond = request =>
        {
            var url = request.RequestUri!.ToString();
            if (url.Contains("oauth2.googleapis.com/tokeninfo", StringComparison.Ordinal))
            {
                return JsonResponse(HttpStatusCode.OK, """{ "scope": "openid email" }""");
            }

            return JsonResponse(HttpStatusCode.Forbidden, """
                { "error": { "code": 403, "message": "Request had insufficient authentication scopes.", "status": "PERMISSION_DENIED" } }
                """);
        };

        var result = await harness.Service.GetAccountDebugAsync();
        Assert.Equal(GoogleCalendarService.RequiredScope, result.StoredScope);
        Assert.Equal("openid email", result.LiveScope);
        Assert.True(result.ReconnectRequired);
        Assert.False(result.ScopeSufficient);
        Assert.Equal(403, result.GoogleStatusCode);
        Assert.Equal("Google", result.Provider);
        Assert.Contains("Live access token", result.ReconnectReason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Authorization_code_exchange_replaces_the_refresh_token()
    {
        await using var harness = await Harness.CreateAsync(
            refreshToken: "OLD_REFRESH_TOKEN",
            accessExpiresAt: DateTimeOffset.UtcNow.AddHours(1));

        harness.Handler.Respond = request =>
        {
            Assert.Equal("https://oauth2.googleapis.com/token", request.RequestUri?.ToString());
            return JsonResponse(HttpStatusCode.OK, """
                {
                  "access_token": "NEW_ACCESS_TOKEN",
                  "refresh_token": "NEW_REFRESH_TOKEN",
                  "expires_in": 3600,
                  "scope": "https://www.googleapis.com/auth/calendar.events",
                  "token_type": "Bearer"
                }
                """);
        };

        await harness.Service.StoreAuthorizationCodeAsync("auth-code");
        var stored = await harness.Db.IntegrationTokens.SingleAsync();
        Assert.Equal("NEW_REFRESH_TOKEN", stored.RefreshToken);
        Assert.Equal("NEW_ACCESS_TOKEN", stored.AccessToken);
        Assert.Equal(GoogleCalendarService.RequiredScope, stored.Scope);
        Assert.DoesNotContain("OLD_REFRESH_TOKEN", stored.RefreshToken, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Authorization_code_exchange_does_not_keep_the_old_refresh_token_when_google_omits_one()
    {
        await using var harness = await Harness.CreateAsync(refreshToken: "OLD_REFRESH_TOKEN");
        harness.Handler.Respond = _ => JsonResponse(HttpStatusCode.OK, """
            {
              "access_token": "NEW_ACCESS_TOKEN",
              "expires_in": 3600,
              "scope": "https://www.googleapis.com/auth/calendar.events",
              "token_type": "Bearer"
            }
            """);

        await Assert.ThrowsAsync<InvalidOperationException>(() => harness.Service.StoreAuthorizationCodeAsync("auth-code"));
        var stored = await harness.Db.IntegrationTokens.SingleAsync();
        Assert.Equal("OLD_REFRESH_TOKEN", stored.RefreshToken);
        Assert.Equal("access-token", stored.AccessToken);
    }

    [Fact]
    public async Task Authorization_code_exchange_inserts_refresh_token_when_no_grant_exists()
    {
        await using var harness = await Harness.CreateAsync(storeGrant: false);
        Assert.False(harness.Service.HasStoredGrant);

        harness.Handler.Respond = request =>
        {
            Assert.Equal("https://oauth2.googleapis.com/token", request.RequestUri?.ToString());
            return JsonResponse(HttpStatusCode.OK, """
                {
                  "access_token": "FIRST_ACCESS_TOKEN",
                  "refresh_token": "FIRST_REFRESH_TOKEN",
                  "expires_in": 3600,
                  "scope": "https://www.googleapis.com/auth/calendar.events",
                  "token_type": "Bearer"
                }
                """);
        };

        await harness.Service.StoreAuthorizationCodeAsync(
            "auth-code",
            GoogleRedirectUri.DockerCallback);

        var stored = await harness.Db.IntegrationTokens.SingleAsync();
        Assert.Equal(GoogleCalendarService.ProviderKey, stored.Provider);
        Assert.Equal("FIRST_REFRESH_TOKEN", stored.RefreshToken);
        Assert.Equal("FIRST_ACCESS_TOKEN", stored.AccessToken);
        Assert.Equal(GoogleCalendarService.RequiredScope, stored.Scope);
        Assert.True(harness.Service.HasStoredGrant);

        var form = Assert.Single(harness.Handler.Bodies);
        Assert.Contains(
            "redirect_uri=" + Uri.EscapeDataString(GoogleRedirectUri.DockerCallback),
            form,
            StringComparison.Ordinal);
        Assert.DoesNotContain("localhost:5001", form, StringComparison.Ordinal);
        Assert.DoesNotContain("FIRST_REFRESH_TOKEN", form, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Authorization_code_exchange_rewrites_leftover_5001_when_REIGN_DOCKER()
    {
        using var env = new EnvScope();
        env.ClearDockerRuntimeMarkers();
        env.Set("REIGN_DOCKER", "1");

        await using var harness = await Harness.CreateAsync(
            storeGrant: false,
            redirectUri: GoogleRedirectUri.KestrelHttpsCallback);
        harness.Handler.Respond = request =>
        {
            Assert.Equal("https://oauth2.googleapis.com/token", request.RequestUri?.ToString());
            return JsonResponse(HttpStatusCode.OK, """
                {
                  "access_token": "DOCKER_ACCESS_TOKEN",
                  "refresh_token": "DOCKER_REFRESH_TOKEN",
                  "expires_in": 3600,
                  "scope": "https://www.googleapis.com/auth/calendar.events",
                  "token_type": "Bearer"
                }
                """);
        };

        await harness.Service.StoreAuthorizationCodeAsync(
            "auth-code",
            GoogleRedirectUri.KestrelHttpsCallback);

        var form = Assert.Single(harness.Handler.Bodies);
        Assert.Contains(
            "redirect_uri=" + Uri.EscapeDataString(GoogleRedirectUri.DockerCallback),
            form,
            StringComparison.Ordinal);
        Assert.DoesNotContain("localhost:5001", form, StringComparison.Ordinal);
        Assert.DoesNotContain("localhost%3A5001", form, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Authorization_code_exchange_does_not_insert_when_google_returns_an_error()
    {
        await using var harness = await Harness.CreateAsync(storeGrant: false);
        harness.Handler.Respond = _ => JsonResponse(HttpStatusCode.BadRequest, """
            {
              "error": "redirect_uri_mismatch",
              "error_description": "redirect_uri must match the authorization request"
            }
            """);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            harness.Service.StoreAuthorizationCodeAsync("auth-code", GoogleRedirectUri.DockerCallback));
        Assert.Equal(0, await harness.Db.IntegrationTokens.CountAsync());
        Assert.False(harness.Service.HasStoredGrant);
    }

    [Fact]
    public async Task Upsert_succeeds_only_after_google_get_back()
    {
        const string eventId = "1tfftt6crrdcaju5iimch6r3lc";
        await using var harness = await Harness.CreateAsync(accessExpiresAt: DateTimeOffset.UtcNow.AddHours(1));
        harness.Handler.Respond = request =>
        {
            var url = request.RequestUri!.ToString();
            if (request.Method == HttpMethod.Get && url.Contains("privateExtendedProperty", StringComparison.Ordinal))
            {
                return JsonResponse(HttpStatusCode.OK, """{ "items": [] }""");
            }

            if (request.Method == HttpMethod.Post && url.EndsWith("/events", StringComparison.Ordinal))
            {
                return JsonResponse(HttpStatusCode.OK, $$"""
                    {
                      "id": "{{eventId}}",
                      "htmlLink": "https://www.google.com/calendar/event?eid=abc",
                      "start": { "dateTime": "2026-08-21T14:00:00", "timeZone": "America/New_York" }
                    }
                    """);
            }

            if (request.Method == HttpMethod.Get && url.EndsWith($"/events/{eventId}", StringComparison.Ordinal))
            {
                return JsonResponse(HttpStatusCode.OK, $$"""
                    {
                      "id": "{{eventId}}",
                      "summary": "REIGN Quick Visit",
                      "htmlLink": "https://www.google.com/calendar/event?eid=abc",
                      "organizer": { "email": "studio@example.com" },
                      "start": { "dateTime": "2026-08-21T14:00:00", "timeZone": "America/New_York" },
                      "end": { "dateTime": "2026-08-21T14:20:00", "timeZone": "America/New_York" }
                    }
                    """);
            }

            return JsonResponse(HttpStatusCode.InternalServerError, """{ "error": "unexpected" }""");
        };

        var start = new DateTime(2026, 8, 21, 14, 0, 0, DateTimeKind.Unspecified);
        var result = await harness.Service.UpsertAppointmentAsync(new CalendarEventRequest
        {
            AppointmentId = Guid.Parse("4ff7c1a6-470c-41d4-b9a5-9a2a906e1ec7"),
            Summary = "REIGN Quick Visit",
            Start = start,
            End = start.AddMinutes(20),
            Status = "Confirmed"
        });

        Assert.True(result.Succeeded);
        Assert.Equal(eventId, result.EventId);
        Assert.Equal("https://www.google.com/calendar/event?eid=abc", result.HtmlLink);
        Assert.Equal("America/New_York", result.TimeZone);
        Assert.Equal("primary", result.CalendarId);
        Assert.Contains(harness.Handler.Bodies, body =>
            body.Contains("\"dateTime\":\"2026-08-21T14:00:00\"", StringComparison.Ordinal) &&
            body.Contains("\"timeZone\":\"America/New_York\"", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Upsert_fails_when_google_returns_403()
    {
        await using var harness = await Harness.CreateAsync(accessExpiresAt: DateTimeOffset.UtcNow.AddHours(1));
        harness.Handler.Respond = request =>
        {
            var url = request.RequestUri!.ToString();
            if (request.Method == HttpMethod.Get && url.Contains("privateExtendedProperty", StringComparison.Ordinal))
            {
                return JsonResponse(HttpStatusCode.OK, """{ "items": [] }""");
            }

            return JsonResponse(HttpStatusCode.Forbidden, """
                { "error": { "code": 403, "message": "Forbidden", "status": "PERMISSION_DENIED" } }
                """);
        };

        var result = await harness.Service.UpsertAppointmentAsync(new CalendarEventRequest
        {
            AppointmentId = Guid.NewGuid(),
            Summary = "REIGN Quick Visit",
            Start = new DateTime(2026, 8, 21, 14, 0, 0, DateTimeKind.Unspecified),
            End = new DateTime(2026, 8, 21, 14, 20, 0, DateTimeKind.Unspecified),
            Status = "Confirmed"
        });

        Assert.False(result.Succeeded);
        Assert.Equal(403, result.GoogleStatusCode);
        Assert.Contains("403", result.Error, StringComparison.Ordinal);
        Assert.Null(result.EventId);
        Assert.DoesNotContain(harness.Handler.Urls, url => url.Contains("/events/evt", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Upsert_fails_when_written_event_cannot_be_retrieved()
    {
        await using var harness = await Harness.CreateAsync(accessExpiresAt: DateTimeOffset.UtcNow.AddHours(1));
        harness.Handler.Respond = request =>
        {
            var url = request.RequestUri!.ToString();
            if (request.Method == HttpMethod.Get && url.Contains("privateExtendedProperty", StringComparison.Ordinal))
            {
                return JsonResponse(HttpStatusCode.OK, """{ "items": [] }""");
            }

            if (request.Method == HttpMethod.Post)
            {
                return JsonResponse(HttpStatusCode.OK, """{ "id": "ghost-event" }""");
            }

            return JsonResponse(HttpStatusCode.NotFound, """{ "error": { "code": 404, "message": "Not Found" } }""");
        };

        var result = await harness.Service.UpsertAppointmentAsync(new CalendarEventRequest
        {
            AppointmentId = Guid.NewGuid(),
            Summary = "REIGN Quick Visit",
            Start = new DateTime(2026, 8, 21, 14, 0, 0, DateTimeKind.Unspecified),
            End = new DateTime(2026, 8, 21, 14, 20, 0, DateTimeKind.Unspecified),
            Status = "Confirmed"
        });

        Assert.False(result.Succeeded);
        Assert.Equal(404, result.GoogleStatusCode);
        Assert.Contains("GET returned HTTP 404", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Upsert_put_404_creates_a_new_event()
    {
        await using var harness = await Harness.CreateAsync(accessExpiresAt: DateTimeOffset.UtcNow.AddHours(1));
        harness.Handler.Respond = request =>
        {
            var url = request.RequestUri!.ToString();
            if (request.Method == HttpMethod.Put)
            {
                return JsonResponse(HttpStatusCode.NotFound, """{ "error": { "code": 404 } }""");
            }

            if (request.Method == HttpMethod.Post)
            {
                return JsonResponse(HttpStatusCode.OK, """{ "id": "new-event", "htmlLink": "https://www.google.com/calendar/event?eid=new" }""");
            }

            if (request.Method == HttpMethod.Get && url.EndsWith("/events/new-event", StringComparison.Ordinal))
            {
                return JsonResponse(HttpStatusCode.OK, """{ "id": "new-event", "htmlLink": "https://www.google.com/calendar/event?eid=new" }""");
            }

            return JsonResponse(HttpStatusCode.InternalServerError, "{}");
        };

        var result = await harness.Service.UpsertAppointmentAsync(new CalendarEventRequest
        {
            AppointmentId = Guid.NewGuid(),
            ExistingEventId = "stale-event",
            Summary = "REIGN Quick Visit",
            Start = new DateTime(2026, 8, 21, 14, 0, 0, DateTimeKind.Unspecified),
            End = new DateTime(2026, 8, 21, 14, 20, 0, DateTimeKind.Unspecified),
            Status = "Confirmed"
        });

        Assert.True(result.Succeeded);
        Assert.Equal("new-event", result.EventId);
        Assert.Contains(harness.Handler.Urls, url => url.Contains("/events/stale-event", StringComparison.Ordinal));
    }

    private static IntegrationsController CreateController(GoogleCalendarService google, string environmentName) =>
        new(
            sms: null!,
            calendar: null!,
            googleCalendar: google,
            google: Options.Create(new GoogleCalendarOptions()),
            smsOptions: Options.Create(new SmsOptions()),
            environment: new StubHostEnvironment { EnvironmentName = environmentName },
            logger: NullLogger<IntegrationsController>.Instance);

    private sealed class StubHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;

        public string ApplicationName { get; set; } = "REIGN.Tests";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
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

        public List<string> Bodies { get; } = [];

        public Func<HttpRequestMessage, HttpResponseMessage> Respond { get; set; } =
            _ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{}", Encoding.UTF8, "application/json")
            };

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Urls.Add(request.RequestUri?.ToString() ?? "");
            if (request.Content != null)
            {
                Bodies.Add(await request.Content.ReadAsStringAsync(cancellationToken));
            }

            return Respond(request);
        }
    }

    private sealed class Harness : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly ReignDbContext _db;

        public ScriptedHandler Handler { get; }
        public GoogleCalendarService Service { get; }
        public ReignDbContext Db => _db;

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
            bool storeGrant = true,
            string? scope = "https://www.googleapis.com/auth/calendar.events",
            string? redirectUri = null)
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
                    TokenType = "Bearer",
                    Scope = scope
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
                    CalendarId = calendarId,
                    RedirectUri = redirectUri ?? GoogleRedirectUri.KestrelHttpsCallback
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

    private sealed class EnvScope : IDisposable
    {
        private readonly List<(string Name, string? Previous)> _previous = [];

        public void Set(string name, string? value)
        {
            if (_previous.All(item => !string.Equals(item.Name, name, StringComparison.Ordinal)))
            {
                _previous.Add((name, Environment.GetEnvironmentVariable(name)));
            }

            Environment.SetEnvironmentVariable(name, value);
        }

        public void ClearDockerRuntimeMarkers()
        {
            Set("REIGN_DOCKER", null);
            Set("DOTNET_RUNNING_IN_CONTAINER", null);
            Set("ASPNETCORE_HTTP_PORTS", null);
            Set("ASPNETCORE_URLS", null);
        }

        public void Dispose()
        {
            foreach (var (name, previous) in _previous)
            {
                Environment.SetEnvironmentVariable(name, previous);
            }
        }
    }
}
