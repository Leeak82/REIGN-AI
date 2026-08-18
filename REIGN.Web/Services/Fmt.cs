using System.Globalization;
namespace REIGN.Web.Services;

/// <summary>
/// Display formatting helpers shared across the REIGN command center UI.
/// </summary>
public static class Fmt
{
    public static string TimeAgo(DateTime utc)
    {
        var time = utc.Kind == DateTimeKind.Utc ? utc : DateTime.SpecifyKind(utc, DateTimeKind.Utc);
        var span = DateTime.UtcNow - time;
        if (span.TotalSeconds < 0) span = TimeSpan.Zero;

        if (span.TotalMinutes < 1) return "just now";
        if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes} min ago";
        if (span.TotalHours < 24) return $"{(int)span.TotalHours} hr ago";
        if (span.TotalDays < 7) return $"{(int)span.TotalDays} d ago";
        return time.ToLocalTime().ToString("MMM d");
    }

    public static string Initials(string? name, string? fallback = null)
    {
        var source = !string.IsNullOrWhiteSpace(name) ? name : fallback;
        if (string.IsNullOrWhiteSpace(source)) return "?";

        var parts = source.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 2)
        {
            return $"{char.ToUpperInvariant(parts[0][0])}{char.ToUpperInvariant(parts[1][0])}";
        }

        var word = parts[0];
        return word.Length == 1
            ? word.ToUpperInvariant()
            : $"{char.ToUpperInvariant(word[0])}{char.ToUpperInvariant(word[1])}";
    }

    public static string DisplayName(string? name, string phone) =>
        string.IsNullOrWhiteSpace(name) ? phone : name;

    public static string Greeting()
    {
        var hour = DateTime.Now.Hour;
        return hour < 12 ? "Good morning" : hour < 18 ? "Good afternoon" : "Good evening";
    }

    public static string Money(decimal value) => "$" + value.ToString("N0", CultureInfo.InvariantCulture);

    public static string AppointmentTime(DateTime time) =>
        time.ToString("MMM d, yyyy h:mm tt");

    public static string Label(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "";
        var words = raw.Replace('_', ' ').Replace('-', ' ').ToLowerInvariant();
        return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(words);
    }
}
