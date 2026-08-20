namespace REIGN.API.Calendar;

public static class CalendarTime
{
    public static TimeZoneInfo Resolve(string? timeZoneId)
    {
        if (!string.IsNullOrWhiteSpace(timeZoneId))
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            }
            catch (TimeZoneNotFoundException)
            {
            }
            catch (InvalidTimeZoneException)
            {
            }
        }

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
        }
        catch
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
            }
            catch
            {
                return TimeZoneInfo.Utc;
            }
        }
    }

    /// <summary>
    /// Google Calendar requires an IANA time zone id. Empty and Windows Eastern ids map to America/New_York.
    /// </summary>
    public static string ToGoogleTimeZoneId(string? configured)
    {
        if (string.IsNullOrWhiteSpace(configured) ||
            configured.Equals("Eastern Standard Time", StringComparison.OrdinalIgnoreCase) ||
            configured.Equals("Eastern Daylight Time", StringComparison.OrdinalIgnoreCase))
        {
            return "America/New_York";
        }

        return configured.Trim();
    }

    /// <summary>
    /// Google Calendar local wall-clock datetime (no Z / offset) paired with an IANA timeZone field.
    /// Unspecified values are treated as already being in the business timezone.
    /// </summary>
    public static string ToWallClockRfc3339(DateTime value, TimeZoneInfo timeZone)
    {
        var local = value.Kind switch
        {
            DateTimeKind.Utc => TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(value, DateTimeKind.Utc), timeZone),
            DateTimeKind.Local => TimeZoneInfo.ConvertTime(value, timeZone),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Unspecified)
        };

        return local.ToString("yyyy-MM-dd'T'HH:mm:ss");
    }
}
