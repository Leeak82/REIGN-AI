using System.Net;
using System.Text.RegularExpressions;
using Npgsql;

namespace REIGN.Data;

/// <summary>
/// Detects and normalizes ConnectionStrings__Reign for PostgreSQL (Npgsql) or local SQLite.
/// </summary>
public static class DatabaseConnection
{
    private static readonly Regex SupabaseDirectHost = new(
        @"^db\.([a-z0-9]+)\.supabase\.co$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static string? ResolveFromEnvironment()
    {
        foreach (var name in new[]
        {
            "ConnectionStrings__Reign",
            "CONNECTIONSTRINGS__REIGN",
            "DATABASE_URL",
            "REIGN_CONNECTION_STRING",
            "SUPABASE_DB_URL"
        })
        {
            var value = Environment.GetEnvironmentVariable(name);
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        foreach (System.Collections.DictionaryEntry entry in Environment.GetEnvironmentVariables())
        {
            var key = entry.Key?.ToString();
            if (key != null
                && key.Equals("ConnectionStrings__Reign", StringComparison.OrdinalIgnoreCase)
                && entry.Value is string value
                && !string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return null;
    }

    public static bool IsPostgreSql(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return false;
        }

        var value = connectionString.Trim();
        if (value.Contains("Data Source", StringComparison.OrdinalIgnoreCase)
            || value.Contains("DataSource", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return value.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase)
            || value.Contains("Host=", StringComparison.OrdinalIgnoreCase)
            || value.Contains("Username=", StringComparison.OrdinalIgnoreCase);
    }

    public static string Normalize(string connectionString)
    {
        var builder = Parse(connectionString.Trim());
        ApplySupabasePooler(builder);
        ApplyRenderDefaults(builder);
        builder.Timeout = Math.Max(builder.Timeout, 30);
        return builder.ConnectionString;
    }

    public static string DescribeEndpoint(string connectionString)
    {
        try
        {
            var builder = Parse(connectionString.Trim());
            var host = string.IsNullOrWhiteSpace(builder.Host) ? "(unknown host)" : builder.Host;
            var database = string.IsNullOrWhiteSpace(builder.Database) ? "(unknown database)" : builder.Database;
            return $"{host}:{builder.Port}/{database}";
        }
        catch
        {
            return "(unparseable ConnectionStrings__Reign)";
        }
    }

    public static NpgsqlConnectionStringBuilder Parse(string connectionString)
    {
        var value = connectionString.Trim();
        if (!value.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase)
            && !value.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
        {
            return new NpgsqlConnectionStringBuilder(value);
        }

        var uri = new Uri(value);
        var username = "";
        var password = "";
        if (!string.IsNullOrEmpty(uri.UserInfo))
        {
            var parts = uri.UserInfo.Split(':', 2);
            username = Uri.UnescapeDataString(parts[0]);
            if (parts.Length > 1)
            {
                password = Uri.UnescapeDataString(parts[1]);
            }
        }

        var database = uri.AbsolutePath.Trim('/');
        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.IsDefaultPort ? 5432 : uri.Port,
            Database = string.IsNullOrWhiteSpace(database) ? "reign" : database,
            Username = username,
            Password = password
        };

        var query = uri.Query.TrimStart('?');
        foreach (var pair in query.Split('&', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var separator = pair.IndexOf('=');
            if (separator <= 0)
            {
                continue;
            }

            var key = pair[..separator];
            var raw = Uri.UnescapeDataString(pair[(separator + 1)..]);
            if (key.Equals("sslmode", StringComparison.OrdinalIgnoreCase)
                && Enum.TryParse<SslMode>(raw, ignoreCase: true, out var sslMode))
            {
                builder.SslMode = sslMode;
            }
        }

        return builder;
    }

    public static void ApplyRenderDefaults(NpgsqlConnectionStringBuilder builder)
    {
        var host = builder.Host ?? "";
        var renderExternal = host.Contains("render.com", StringComparison.OrdinalIgnoreCase);
        var supabase = host.Contains("supabase.co", StringComparison.OrdinalIgnoreCase)
            || host.Contains("supabase.com", StringComparison.OrdinalIgnoreCase);
        var renderInternal = host.StartsWith("dpg-", StringComparison.OrdinalIgnoreCase) && !renderExternal;

        if (renderExternal || supabase)
        {
            builder.SslMode = SslMode.Require;
            return;
        }

        if (renderInternal && builder.SslMode == SslMode.Prefer)
        {
            // Internal Render hostnames are private DNS and do not use public TLS.
            builder.SslMode = SslMode.Disable;
        }
    }

    /// <summary>
    /// Direct db.&lt;ref&gt;.supabase.co:5432 is IPv6-only on many projects. Render cannot
    /// open that socket (Network is unreachable). Rewrite to the Session pooler (IPv4, port 5432).
    /// Transaction pooler port 6543 is also rewritten to 5432 so EF Core prepared statements work.
    /// </summary>
    public static void ApplySupabasePooler(NpgsqlConnectionStringBuilder builder)
    {
        var host = builder.Host ?? "";
        if (host.Contains("pooler.supabase.com", StringComparison.OrdinalIgnoreCase))
        {
            if (builder.Port == 6543)
            {
                builder.Port = 5432;
            }

            builder.SslMode = SslMode.Require;
            return;
        }

        var match = SupabaseDirectHost.Match(host);
        if (!match.Success)
        {
            return;
        }

        var projectRef = match.Groups[1].Value;
        var poolerHost = Environment.GetEnvironmentVariable("SUPABASE_POOLER_HOST");
        if (string.IsNullOrWhiteSpace(poolerHost))
        {
            var region = InferSupabaseRegion();
            poolerHost = $"aws-0-{region}.pooler.supabase.com";
        }

        builder.Host = poolerHost.Trim();
        builder.Port = 5432;
        builder.SslMode = SslMode.Require;

        if (string.IsNullOrWhiteSpace(builder.Username)
            || builder.Username.Equals("postgres", StringComparison.OrdinalIgnoreCase))
        {
            builder.Username = $"postgres.{projectRef}";
        }
    }

    public static string InferSupabaseRegion()
    {
        var configured = Environment.GetEnvironmentVariable("SUPABASE_REGION");
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return configured.Trim();
        }

        // Matches the IPv6 prefix 2600:1f14 on this project's direct host.
        return "us-west-2";
    }

    public static string? RegionFromAddress(IPAddress address)
    {
        if (address.AddressFamily != AddressFamily.InterNetworkV6)
        {
            return null;
        }

        var value = address.ToString();
        if (value.StartsWith("2600:1f14:", StringComparison.OrdinalIgnoreCase))
        {
            return "us-west-2";
        }

        if (value.StartsWith("2600:1f18:", StringComparison.OrdinalIgnoreCase))
        {
            return "us-west-1";
        }

        if (value.StartsWith("2600:1f13:", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("2600:1f10:", StringComparison.OrdinalIgnoreCase))
        {
            return "us-east-1";
        }

        if (value.StartsWith("2a05:d014:", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("2a05:d018:", StringComparison.OrdinalIgnoreCase))
        {
            return "eu-west-1";
        }

        return null;
    }

    public static string UnreachableMessage(string endpoint)
    {
        if (endpoint.Contains("supabase", StringComparison.OrdinalIgnoreCase)
            || endpoint.Contains("pooler", StringComparison.OrdinalIgnoreCase))
        {
            return
                $"Cannot reach the database at {endpoint}. Direct db.*.supabase.co:5432 is IPv6-only and unreachable from Render. Use the Supabase Session pooler (aws-0-<region>.pooler.supabase.com port 5432, username postgres.<project-ref>), or set SUPABASE_REGION / SUPABASE_POOLER_HOST if the automatic rewrite picked the wrong region. Do not use the Transaction pooler on port 6543 with Entity Framework. Do not use localhost.";
        }

        return
            $"Cannot reach the database at {endpoint}. On Render, set ConnectionStrings__Reign to the Internal Database URL from a PostgreSQL instance in the same region as this service. Do not use localhost. External *.render.com URLs require SSL.";
    }
}
