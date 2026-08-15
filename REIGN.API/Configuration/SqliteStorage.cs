namespace REIGN.API.Configuration;

/// <summary>
/// SQLite will not create missing parent directories. Render's /data volume path
/// must exist and be writable before MigrateAsync runs.
/// </summary>
public static class SqliteStorage
{
    public static string EnsureWritableFile(string connectionString, string fallbackDirectory, out string? warning)
    {
        warning = null;
        var dataSource = ReadDataSource(connectionString);
        if (string.IsNullOrWhiteSpace(dataSource) || dataSource == ":memory:")
        {
            return connectionString;
        }

        var fullPath = Path.IsPathRooted(dataSource)
            ? dataSource
            : Path.GetFullPath(dataSource);
        var directory = Path.GetDirectoryName(fullPath);
        if (string.IsNullOrWhiteSpace(directory))
        {
            return RewriteDataSource(connectionString, fullPath);
        }

        if (TryEnsureDirectory(directory))
        {
            return RewriteDataSource(connectionString, Path.Combine(directory, Path.GetFileName(fullPath)));
        }

        Directory.CreateDirectory(fallbackDirectory);
        var fallback = Path.Combine(fallbackDirectory, Path.GetFileName(fullPath));
        warning =
            $"SQLite directory '{directory}' is missing or not writable. Using '{fallback}'. Mount a disk at the configured path for durable data.";
        return RewriteDataSource(connectionString, fallback);
    }

    public static string? ReadDataSource(string connectionString)
    {
        foreach (var part in connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var separator = part.IndexOf('=');
            if (separator <= 0)
            {
                continue;
            }

            var key = part[..separator].Trim();
            if (key.Equals("Data Source", StringComparison.OrdinalIgnoreCase)
                || key.Equals("DataSource", StringComparison.OrdinalIgnoreCase))
            {
                return part[(separator + 1)..].Trim();
            }
        }

        return null;
    }

    private static bool TryEnsureDirectory(string directory)
    {
        try
        {
            Directory.CreateDirectory(directory);
            var probe = Path.Combine(directory, ".reign-write-probe");
            File.WriteAllText(probe, "ok");
            File.Delete(probe);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string RewriteDataSource(string connectionString, string dataSource)
    {
        var parts = connectionString
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(part =>
            {
                var separator = part.IndexOf('=');
                if (separator <= 0)
                {
                    return part;
                }

                var key = part[..separator].Trim();
                if (key.Equals("Data Source", StringComparison.OrdinalIgnoreCase)
                    || key.Equals("DataSource", StringComparison.OrdinalIgnoreCase))
                {
                    return $"Data Source={dataSource}";
                }

                return part;
            });

        return string.Join(';', parts);
    }
}
