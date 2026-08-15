using Npgsql;

namespace REIGN.Data;

/// <summary>
/// Detects and normalizes ConnectionStrings__Reign for PostgreSQL (Npgsql) or local SQLite.
/// </summary>
public static class DatabaseConnection
{
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
        var supabase = host.Contains("supabase.co", StringComparison.OrdinalIgnoreCase);
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
}
