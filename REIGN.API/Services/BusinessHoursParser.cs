using System.Globalization;
using System.Text.RegularExpressions;

namespace REIGN.API.Services;

public sealed class BusinessHoursWindow
{
    public TimeSpan OpensAt { get; init; } = TimeSpan.FromHours(9);
    public TimeSpan ClosesAt { get; init; } = TimeSpan.FromHours(17);
    public HashSet<DayOfWeek> OpenDays { get; init; } = Enum.GetValues<DayOfWeek>().ToHashSet();
    public int SameDayNoticeMinutes { get; init; }

    public bool IsOpen(DayOfWeek day) => OpenDays.Contains(day);
}

public static class BusinessHoursParser
{
    private static readonly Regex Range = new(
        @"(?<h1>\d{1,2})(?::(?<m1>\d{2}))?\s*(?<a1>a\.?m\.?|p\.?m\.?)\s*(?:-|–|—|to)\s*(?<h2>\d{1,2})(?::(?<m2>\d{2}))?\s*(?<a2>a\.?m\.?|p\.?m\.?)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Dictionary<string, DayOfWeek> DayNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["sun"] = DayOfWeek.Sunday,
        ["sunday"] = DayOfWeek.Sunday,
        ["mon"] = DayOfWeek.Monday,
        ["monday"] = DayOfWeek.Monday,
        ["tue"] = DayOfWeek.Tuesday,
        ["tues"] = DayOfWeek.Tuesday,
        ["tuesday"] = DayOfWeek.Tuesday,
        ["wed"] = DayOfWeek.Wednesday,
        ["wednesday"] = DayOfWeek.Wednesday,
        ["thu"] = DayOfWeek.Thursday,
        ["thur"] = DayOfWeek.Thursday,
        ["thurs"] = DayOfWeek.Thursday,
        ["thursday"] = DayOfWeek.Thursday,
        ["fri"] = DayOfWeek.Friday,
        ["friday"] = DayOfWeek.Friday,
        ["sat"] = DayOfWeek.Saturday,
        ["saturday"] = DayOfWeek.Saturday
    };

    public static BusinessHoursWindow Parse(string? hours)
    {
        var text = (hours ?? string.Empty).Trim();
        var opens = TimeSpan.FromHours(9);
        var closes = TimeSpan.FromHours(17);
        var match = Range.Match(text);
        if (match.Success &&
            TryTime(match.Groups["h1"].Value, match.Groups["m1"].Value, match.Groups["a1"].Value, out var parsedOpen) &&
            TryTime(match.Groups["h2"].Value, match.Groups["m2"].Value, match.Groups["a2"].Value, out var parsedClose) &&
            parsedClose > parsedOpen)
        {
            opens = parsedOpen;
            closes = parsedClose;
        }

        var days = ParseDays(text);
        var notice = ParseSameDayNotice(text);
        return new BusinessHoursWindow
        {
            OpensAt = opens,
            ClosesAt = closes,
            OpenDays = days,
            SameDayNoticeMinutes = notice
        };
    }

    private static HashSet<DayOfWeek> ParseDays(string text)
    {
        var lower = text.ToLowerInvariant();
        if (lower.Contains("weekday"))
        {
            return new HashSet<DayOfWeek>
            {
                DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday,
                DayOfWeek.Thursday, DayOfWeek.Friday
            };
        }

        if (lower.Contains("weekend"))
        {
            return new HashSet<DayOfWeek> { DayOfWeek.Saturday, DayOfWeek.Sunday };
        }

        var range = Regex.Match(lower,
            @"\b(mon(?:day)?|tue(?:sday|s)?|wed(?:nesday)?|thu(?:rsday|rs)?|fri(?:day)?|sat(?:urday)?|sun(?:day)?)\s*(?:-|–|—|to)\s*(mon(?:day)?|tue(?:sday|s)?|wed(?:nesday)?|thu(?:rsday|rs)?|fri(?:day)?|sat(?:urday)?|sun(?:day)?)\b");
        if (range.Success && TryDay(range.Groups[1].Value, out var start) && TryDay(range.Groups[2].Value, out var end))
        {
            var result = new HashSet<DayOfWeek>();
            var current = start;
            for (var i = 0; i < 7; i++)
            {
                result.Add(current);
                if (current == end) break;
                current = (DayOfWeek)(((int)current + 1) % 7);
            }
            return result;
        }

        var explicitDays = new HashSet<DayOfWeek>();
        foreach (Match dayMatch in Regex.Matches(lower,
                     @"\b(sunday|sun|monday|mon|tuesday|tues|tue|wednesday|wed|thursday|thurs|thur|thu|friday|fri|saturday|sat)\b"))
        {
            if (TryDay(dayMatch.Value, out var day))
            {
                explicitDays.Add(day);
            }
        }

        return explicitDays.Count > 0
            ? explicitDays
            : Enum.GetValues<DayOfWeek>().ToHashSet();
    }

    private static int ParseSameDayNotice(string text)
    {
        var lower = text.ToLowerInvariant();
        if (!lower.Contains("notice") && !lower.Contains("advance")) return 0;

        if (Regex.IsMatch(lower, @"\b(one|1)\s+hour")) return 60;
        var hours = Regex.Match(lower, @"\b(\d{1,2})\s+hours?\b");
        if (hours.Success && int.TryParse(hours.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed))
        {
            return Math.Clamp(parsed * 60, 0, 24 * 60);
        }

        var minutes = Regex.Match(lower, @"\b(\d{1,3})\s+minutes?\b");
        return minutes.Success && int.TryParse(minutes.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var mins)
            ? Math.Clamp(mins, 0, 24 * 60)
            : 0;
    }

    private static bool TryDay(string value, out DayOfWeek day)
    {
        var key = value.Trim().ToLowerInvariant();
        return DayNames.TryGetValue(key, out day);
    }

    private static bool TryTime(string hourText, string minuteText, string amPm, out TimeSpan time)
    {
        time = default;
        if (!int.TryParse(hourText, out var hour)) return false;
        var minute = string.IsNullOrWhiteSpace(minuteText) ? 0 : int.Parse(minuteText, CultureInfo.InvariantCulture);
        var modifier = amPm.Replace(".", string.Empty, StringComparison.Ordinal).ToLowerInvariant();
        if (hour is < 1 or > 12 || minute is < 0 or > 59) return false;
        if (modifier == "pm" && hour < 12) hour += 12;
        if (modifier == "am" && hour == 12) hour = 0;
        time = new TimeSpan(hour, minute, 0);
        return true;
    }
}
