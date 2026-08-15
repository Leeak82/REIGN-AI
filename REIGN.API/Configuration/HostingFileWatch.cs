using Microsoft.Extensions.Configuration;

namespace REIGN.API.Configuration;

/// <summary>
/// Render and similar Linux hosts cap inotify at 128 instances.
/// ASP.NET Core's default appsettings reloadOnChange watchers exceed that and crash startup.
/// </summary>
public static class HostingFileWatch
{
    public static void DisableForProductionHosts()
    {
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
            ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");
        var onRender = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("RENDER"));

        if (onRender && string.IsNullOrWhiteSpace(environment))
        {
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Production");
        }

        if (onRender || !string.Equals(environment, "Development", StringComparison.OrdinalIgnoreCase))
        {
            Environment.SetEnvironmentVariable("DOTNET_HOSTBUILDER__RELOADCONFIGONCHANGE", "false");
            Environment.SetEnvironmentVariable("ASPNETCORE_hostBuilder__reloadConfigOnChange", "false");
            Environment.SetEnvironmentVariable("DOTNET_USE_POLLING_FILE_WATCHER", "true");
        }

        if (onRender)
        {
            // Supabase pooler is dual-stack; prefer IPv4 so Npgsql does not try AAAA first.
            Environment.SetEnvironmentVariable("DOTNET_SYSTEM_NET_DISABLEIPV6", "1");
        }
    }

    public static void DisableReloadOnChange(ConfigurationManager configuration)
    {
        foreach (var source in configuration.Sources)
        {
            if (source is FileConfigurationSource file)
            {
                file.ReloadOnChange = false;
            }
        }
    }
}
