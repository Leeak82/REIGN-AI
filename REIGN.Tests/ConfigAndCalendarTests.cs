using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using REIGN.API.Calendar;
using REIGN.API.Configuration;
using Xunit;

namespace REIGN.Tests;

public class ConfigAndCalendarTests
{
    [Fact]
    public void Alias_copies_env_only_when_configuration_key_is_empty()
    {
        var previous = Environment.GetEnvironmentVariable("REIGN_TEST_GROQ_ALIAS");
        try
        {
            Environment.SetEnvironmentVariable("REIGN_TEST_GROQ_ALIAS", "from-env");
            var empty = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Ai:ApiKey"] = ""
            }).Build();
            var extras = new Dictionary<string, string?>();
            ConfigEnvironmentAliases.TryAlias(empty, extras, "Ai:ApiKey", "REIGN_TEST_GROQ_ALIAS");
            Assert.Equal("from-env", extras["Ai:ApiKey"]);

            extras.Clear();
            var present = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Ai:ApiKey"] = "already-set"
            }).Build();
            ConfigEnvironmentAliases.TryAlias(present, extras, "Ai:ApiKey", "REIGN_TEST_GROQ_ALIAS");
            Assert.Empty(extras);
        }
        finally
        {
            Environment.SetEnvironmentVariable("REIGN_TEST_GROQ_ALIAS", previous);
        }
    }

    [Fact]
    public void Startup_validator_reports_missing_credentials_without_printing_secret_values()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Sms:Provider"] = "Twilio",
            ["GoogleCalendar:Provider"] = "Google",
            ["Ai:ApiKey"] = "super-secret-groq-key",
            ["Sms:Twilio:AuthToken"] = "super-secret-twilio-token"
        }).Build();

        var logger = new ListLogger();
        ConfigStartupValidator.Validate(configuration, logger, isProduction: true);

        Assert.Contains(logger.Messages, m => m.Contains("Groq API key is present", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(logger.Messages, m => m.Contains("Twilio", StringComparison.OrdinalIgnoreCase) && m.Contains("missing", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(logger.Messages, m => m.Contains("GoogleCalendar__ClientId", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(logger.Messages, m => m.Contains("REIGN startup status", StringComparison.Ordinal));
        Assert.Contains(logger.Messages, m => m.Contains("database=not configured", StringComparison.Ordinal));
        Assert.Contains(logger.Messages, m => m.Contains("groq=configured", StringComparison.Ordinal));
        Assert.Contains(logger.Messages, m => m.Contains("sms=not configured", StringComparison.Ordinal));
        Assert.Contains(logger.Messages, m => m.Contains("calendar=not configured", StringComparison.Ordinal));
        Assert.DoesNotContain(logger.Messages, m => m.Contains("super-secret-groq-key"));
        Assert.DoesNotContain(logger.Messages, m => m.Contains("super-secret-twilio-token"));
    }

    [Fact]
    public void Development_cors_includes_localhost_and_rejects_wildcard()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Cors:AllowedOrigins"] = "https://app.example.com, *"
        }).Build();

        var resolved = CorsOriginPolicy.Resolve(configuration, isDevelopment: true);

        Assert.True(resolved.RejectedWildcard);
        Assert.Contains("http://localhost:5012", resolved.Origins);
        Assert.Contains("https://localhost:5001", resolved.Origins);
        Assert.Contains("https://app.example.com", resolved.Origins);
        Assert.DoesNotContain("*", resolved.Origins);
    }

    [Fact]
    public void Production_cors_uses_configured_origins_only_and_rejects_wildcard()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Cors:AllowedOrigins"] = "*, https://reign.example.com/"
        }).Build();

        var resolved = CorsOriginPolicy.Resolve(configuration, isDevelopment: false);

        Assert.True(resolved.RejectedWildcard);
        Assert.Equal(new[] { "https://reign.example.com" }, resolved.Origins);
        Assert.DoesNotContain(resolved.Origins, origin => origin.Contains("localhost", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Production_cors_without_origins_does_not_allow_any_origin()
    {
        var configuration = new ConfigurationBuilder().Build();
        var resolved = CorsOriginPolicy.Resolve(configuration, isDevelopment: false);
        Assert.False(resolved.RejectedWildcard);
        Assert.Empty(resolved.Origins);
    }

    [Fact]
    public void Container_listen_accepts_valid_port_values_only()
    {
        var previousPort = Environment.GetEnvironmentVariable("PORT");
        var previousAzure = Environment.GetEnvironmentVariable("WEBSITES_PORT");
        try
        {
            Environment.SetEnvironmentVariable("WEBSITES_PORT", null);

            Environment.SetEnvironmentVariable("PORT", "8080");
            Assert.True(ContainerListen.TryGetPort(out var port));
            Assert.Equal(8080, port);

            Environment.SetEnvironmentVariable("PORT", "not-a-port");
            Assert.False(ContainerListen.TryGetPort(out _));
        }
        finally
        {
            Environment.SetEnvironmentVariable("PORT", previousPort);
            Environment.SetEnvironmentVariable("WEBSITES_PORT", previousAzure);
        }
    }

    [Fact]
    public void Calendar_wall_clock_keeps_unspecified_local_time_and_converts_utc()
    {
        var tz = CalendarTime.Resolve("America/New_York");
        var unspecified = new DateTime(2026, 8, 15, 14, 0, 0, DateTimeKind.Unspecified);
        Assert.Equal("2026-08-15T14:00:00", CalendarTime.ToWallClockRfc3339(unspecified, tz));
        Assert.DoesNotContain("Z", CalendarTime.ToWallClockRfc3339(unspecified, tz));

        if (tz.Id is "America/New_York" or "Eastern Standard Time")
        {
            var utc = new DateTime(2026, 8, 15, 18, 0, 0, DateTimeKind.Utc);
            Assert.Equal("2026-08-15T14:00:00", CalendarTime.ToWallClockRfc3339(utc, tz));
        }
    }

    private sealed class ListLogger : ILogger
    {
        public List<string> Messages { get; } = [];

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            Messages.Add($"{logLevel}: {formatter(state, exception)}");
        }

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();

            public void Dispose()
            {
            }
        }
    }
}
