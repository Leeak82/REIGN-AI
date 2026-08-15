using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using REIGN.API.Calendar;
using REIGN.API.Configuration;
using REIGN.Data;
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
    public void Database_connection_detects_postgres_and_leaves_sqlite_as_local_fallback()
    {
        Assert.True(DatabaseConnection.IsPostgreSql("Host=localhost;Database=reign;Username=postgres;Password=postgres"));
        Assert.True(DatabaseConnection.IsPostgreSql("postgresql://reign:secret@dpg-xxxx-a/reign"));
        Assert.False(DatabaseConnection.IsPostgreSql("Data Source=/data/REIGN.db"));
        Assert.False(DatabaseConnection.IsPostgreSql(""));

        var previous = Environment.GetEnvironmentVariable("DATABASE_URL");
        try
        {
            Environment.SetEnvironmentVariable("DATABASE_URL", "Host=db.example.supabase.co;Database=postgres;Username=postgres;Password=x");
            Assert.Contains("supabase.co", DatabaseConnection.ResolveFromEnvironment());
        }
        finally
        {
            Environment.SetEnvironmentVariable("DATABASE_URL", previous);
        }

        var external = DatabaseConnection.Normalize("postgresql://reign:s3cret@dpg-xxxx.render.com/reign");
        Assert.Contains("Host=dpg-xxxx.render.com", external);
        Assert.Contains("Database=reign", external);
        Assert.Contains("Username=reign", external);
        Assert.Contains("SSL Mode=Require", external);

        var previousRegion = Environment.GetEnvironmentVariable("SUPABASE_REGION");
        var previousPooler = Environment.GetEnvironmentVariable("SUPABASE_POOLER_HOST");
        string supabase;
        try
        {
            Environment.SetEnvironmentVariable("SUPABASE_REGION", null);
            Environment.SetEnvironmentVariable("SUPABASE_POOLER_HOST", null);
            supabase = DatabaseConnection.Normalize(
                "Host=db.example.supabase.co;Port=5432;Database=postgres;Username=postgres;Password=placeholder;SSL Mode=Prefer");
        }
        finally
        {
            Environment.SetEnvironmentVariable("SUPABASE_REGION", previousRegion);
            Environment.SetEnvironmentVariable("SUPABASE_POOLER_HOST", previousPooler);
        }

        Assert.Contains("SSL Mode=Require", supabase);
        Assert.Contains("Host=aws-0-us-west-2.pooler.supabase.com", supabase);
        Assert.Contains("Username=postgres.example", supabase);
        Assert.Contains("Port=5432", supabase);
        Assert.DoesNotContain("db.example.supabase.co", supabase);

        var internalUrl = DatabaseConnection.Parse("postgresql://reign:s3cret@dpg-xxxx-a/reign");
        DatabaseConnection.ApplyRenderDefaults(internalUrl);
        Assert.Equal("dpg-xxxx-a", internalUrl.Host);
        Assert.Equal(Npgsql.SslMode.Disable, internalUrl.SslMode);

        Assert.Equal("reign@dpg-xxxx-a:5432/reign", DatabaseConnection.DescribeEndpoint("postgresql://reign:s3cret@dpg-xxxx-a/reign"));
        Assert.DoesNotContain("s3cret", DatabaseConnection.DescribeEndpoint("postgresql://reign:s3cret@dpg-xxxx-a/reign"));
    }

    [Fact]
    public void Supabase_direct_host_rewrites_to_session_pooler()
    {
        var previousRegion = Environment.GetEnvironmentVariable("SUPABASE_REGION");
        var previousPooler = Environment.GetEnvironmentVariable("SUPABASE_POOLER_HOST");
        var previousRef = Environment.GetEnvironmentVariable("SUPABASE_PROJECT_REF");
        try
        {
            Environment.SetEnvironmentVariable("SUPABASE_POOLER_HOST", null);
            Environment.SetEnvironmentVariable("SUPABASE_REGION", null);
            Environment.SetEnvironmentVariable("SUPABASE_PROJECT_REF", null);

            var rewritten = DatabaseConnection.Normalize(
                "Host=db.abcdefghijklmnopabcd.supabase.co;Port=5432;Database=postgres;Username=postgres;Password=placeholder;SSL Mode=Prefer");
            Assert.Contains("Host=aws-0-us-west-2.pooler.supabase.com", rewritten);
            Assert.Contains("Username=postgres.abcdefghijklmnopabcd", rewritten);
            Assert.Contains("Port=5432", rewritten);
            Assert.Contains("SSL Mode=Require", rewritten);
            Assert.DoesNotContain("db.abcdefghijklmnopabcd.supabase.co", rewritten);
            Assert.DoesNotContain("placeholder", DatabaseConnection.DescribeEndpoint(rewritten));

            var uri = DatabaseConnection.Normalize(
                "postgresql://postgres:s3cret@db.abc123.supabase.co:5432/postgres");
            Assert.Contains("Host=aws-0-us-west-2.pooler.supabase.com", uri);
            Assert.Contains("Username=postgres.abc123", uri);

            Environment.SetEnvironmentVariable("SUPABASE_REGION", "eu-west-1");
            var eu = DatabaseConnection.Normalize(
                "Host=db.abc123.supabase.co;Database=postgres;Username=postgres;Password=x");
            Assert.Contains("Host=aws-0-eu-west-1.pooler.supabase.com", eu);

            Environment.SetEnvironmentVariable("SUPABASE_REGION", null);
            Environment.SetEnvironmentVariable("SUPABASE_POOLER_HOST", "aws-0-us-east-1.pooler.supabase.com");
            var overrideHost = DatabaseConnection.Normalize(
                "Host=db.abc123.supabase.co;Database=postgres;Username=postgres;Password=x");
            Assert.Contains("Host=aws-0-us-east-1.pooler.supabase.com", overrideHost);

            var alreadyPooled = DatabaseConnection.Normalize(
                "Host=aws-0-us-west-2.pooler.supabase.com;Port=6543;Database=postgres;Username=postgres.abc123;Password=x");
            Assert.Contains("Host=aws-0-us-west-2.pooler.supabase.com", alreadyPooled);
            Assert.Contains("Port=5432", alreadyPooled);
            Assert.Contains("Username=postgres.abc123", alreadyPooled);
            Assert.Equal(Npgsql.GssEncryptionMode.Disable, DatabaseConnection.Parse(alreadyPooled).GssEncryptionMode);

            Environment.SetEnvironmentVariable("SUPABASE_PROJECT_REF", "fromenvref");
            var poolerBareUser = DatabaseConnection.Normalize(
                "Host=aws-0-us-west-2.pooler.supabase.com;Port=5432;Database=postgres;Username=postgres;Password=x");
            Assert.Contains("Username=postgres.fromenvref", poolerBareUser);
            Environment.SetEnvironmentVariable("SUPABASE_PROJECT_REF", null);

            Assert.Contains("database password", DatabaseConnection.AuthFailedMessage("postgres.abc@pooler/postgres"));

            var quoted = DatabaseConnection.Normalize(
                "\"Host=db.abc123.supabase.co;Database=postgres;Username=postgres;Password=s3cret\"");
            Assert.Contains("Password=s3cret", quoted);
            Assert.DoesNotContain("Password=\"s3cret\"", quoted);
            Assert.Equal("database password is set (6 characters)", DatabaseConnection.DescribeSecret(quoted));

            var previousPassword = Environment.GetEnvironmentVariable("SUPABASE_DB_PASSWORD");
            try
            {
                Environment.SetEnvironmentVariable("SUPABASE_DB_PASSWORD", "\"override-secret\"");
                var overridden = DatabaseConnection.Normalize(
                    "Host=db.abc123.supabase.co;Database=postgres;Username=postgres;Password=old");
                Assert.Contains("Password=override-secret", overridden);
                Assert.DoesNotContain("Password=old", overridden);
            }
            finally
            {
                Environment.SetEnvironmentVariable("SUPABASE_DB_PASSWORD", previousPassword);
            }
            Assert.True(DatabaseConnection.IsPasswordAuthFailure(
                new InvalidOperationException("wrapper", new Exception("28P01: password authentication failed for user \"postgres\""))));

            var customUser = DatabaseConnection.Normalize(
                "Host=db.abc123.supabase.co;Database=postgres;Username=postgres.abc123;Password=x");
            Assert.Contains("Username=postgres.abc123", customUser);

            Assert.Equal(
                "us-west-2",
                DatabaseConnection.RegionFromAddress(System.Net.IPAddress.Parse("2600:1f14:90b:6000:bda3:eaaf:4da0:216c")));
            Assert.Equal(
                "us-east-1",
                DatabaseConnection.RegionFromAddress(System.Net.IPAddress.Parse("2600:1f13::1")));
            Assert.Null(DatabaseConnection.RegionFromAddress(System.Net.IPAddress.Parse("1.2.3.4")));

            Assert.Contains(
                "IPv6-only",
                DatabaseConnection.UnreachableMessage("aws-0-us-west-2.pooler.supabase.com:5432/postgres"));
            Assert.Contains(
                "Internal Database URL",
                DatabaseConnection.UnreachableMessage("dpg-xxxx-a:5432/reign"));
        }
        finally
        {
            Environment.SetEnvironmentVariable("SUPABASE_REGION", previousRegion);
            Environment.SetEnvironmentVariable("SUPABASE_POOLER_HOST", previousPooler);
            Environment.SetEnvironmentVariable("SUPABASE_PROJECT_REF", previousRef);
        }
    }

    [Fact]
    public void Sqlite_storage_creates_missing_parent_directory()
    {
        var root = Path.Combine(Path.GetTempPath(), "reign-sqlite-" + Guid.NewGuid().ToString("N"));
        var file = Path.Combine(root, "data", "REIGN.db");
        try
        {
            var resolved = SqliteStorage.EnsureWritableFile($"Data Source={file}", Path.GetTempPath(), out var warning);
            Assert.Null(warning);
            Assert.Contains("REIGN.db", resolved, StringComparison.Ordinal);
            Assert.True(Directory.Exists(Path.GetDirectoryName(file)));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
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
        Assert.DoesNotContain(logger.Messages, m => m.Contains("super-secret-groq-key"));
        Assert.DoesNotContain(logger.Messages, m => m.Contains("super-secret-twilio-token"));
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
