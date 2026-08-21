using Microsoft.Extensions.Options;
using REIGN.API.Options;

namespace REIGN.API.Calendar;

/// <summary>
/// Wall-clock time for Miss Reign's business (Pierce / King County, Pacific by default).
/// Appointment values are stored as Unspecified local times in this zone.
/// </summary>
public sealed class BusinessClock
{
    public const string DefaultTimeZoneId = "America/Los_Angeles";

    public BusinessClock()
        : this(DefaultTimeZoneId)
    {
    }

    public BusinessClock(IOptions<GoogleCalendarOptions> options)
        : this(options.Value.TimeZone)
    {
    }

    public BusinessClock(string? timeZoneId)
    {
        TimeZoneId = CalendarTime.ToGoogleTimeZoneId(timeZoneId);
        TimeZone = CalendarTime.Resolve(TimeZoneId);
    }

    public string TimeZoneId { get; }

    public TimeZoneInfo TimeZone { get; }

    public DateTime Now
    {
        get
        {
            var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZone);
            return DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
        }
    }

    public DateTime Today => Now.Date;
}
