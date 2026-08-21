namespace REIGN.Web;

/// <summary>
/// Honors host-injected container ports (Render PORT, Azure WEBSITES_PORT).
/// </summary>
public static class ContainerListen
{
    public static bool TryGetPort(out int port)
    {
        port = 0;
        var raw = Environment.GetEnvironmentVariable("PORT")
            ?? Environment.GetEnvironmentVariable("WEBSITES_PORT");
        return !string.IsNullOrWhiteSpace(raw)
            && int.TryParse(raw, out port)
            && port is >= 1 and <= 65535;
    }

    public static void Apply(WebApplicationBuilder builder)
    {
        if (!TryGetPort(out var port))
        {
            return;
        }

        builder.WebHost.UseUrls($"http://+:{port}");
    }
}
