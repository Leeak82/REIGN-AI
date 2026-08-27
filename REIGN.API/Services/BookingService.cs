using System.Globalization;
using System.Text.RegularExpressions;
using REIGN.API.Calendar;
using REIGN.Core.Catalog;
using REIGN.Core.Contact;
using REIGN.Core.Models;
using REIGN.Data;
using Microsoft.EntityFrameworkCore;

namespace REIGN.API.Services;

public class BookingService
{
    private static readonly string[] Weekdays =
    [
        "sunday", "monday", "tuesday", "wednesday", "thursday", "friday", "saturday"
    ];

    private static readonly string[] MonthNames =
    [
        "january", "february", "march", "april", "may", "june",
        "july", "august", "september", "october", "november", "december"
    ];

    private readonly ReignDbContext _db;
    private readonly BusinessClock _clock;

    public BookingService(ReignDbContext db, BusinessClock? clock = null)
    {
        _db = db;
        _clock = clock ?? new BusinessClock();
    }

    public async Task<AppointmentRequest> ParseRequest(string message, DateTime? preferredDay = null)
    {
        var text = message.ToLowerInvariant();
        var request = new AppointmentRequest();

        request.ServiceName = MatchCatalogService(text) ?? "";

        if (string.IsNullOrWhiteSpace(request.ServiceName))
        {
            var services = await _db.Services.Where(x => x.Active).ToListAsync();
            foreach (var service in services)
            {
                if (text.Contains(service.Name.ToLowerInvariant()))
                {
                    request.ServiceName = service.Name;
                    break;
                }
            }
        }

        var today = _clock.Today;
        var baseDate = preferredDay?.Date ?? today;
        var parsedDay = TryParseDay(text, today);
        if (parsedDay != null)
        {
            baseDate = parsedDay.Value;
        }

        if (TryParseTime(text, out var hour, out var minute))
        {
            request.RequestedDate = baseDate.AddHours(hour).AddMinutes(minute);
            request.HasTime = true;
        }
        else if (parsedDay != null)
        {
            request.RequestedDate = parsedDay.Value;
            request.HasTime = false;
        }
        else if (text.Contains("tonight") && preferredDay == null)
        {
            request.RequestedDate = today.AddHours(18);
            request.HasTime = true;
        }

        return request;
    }

    public static string? MatchCatalogService(string text)
    {
        var value = text.ToLowerInvariant();

        if (value.Contains("qv") || value.Contains("quick visit") || value.Contains("quick"))
        {
            return ServiceCatalog.QuickVisitName;
        }

        if (value.Contains("hh") || value.Contains("half hour") || value.Contains("half") ||
            value.Contains("30 min") || value.Contains("30-minute"))
        {
            return ServiceCatalog.HalfHourName;
        }

        if (value.Contains("hr") || Regex.IsMatch(value, @"\bhour\b"))
        {
            return ServiceCatalog.HourName;
        }

        return null;
    }

    public string CreateBooking(AppointmentRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ServiceName))
        {
            return $"What service would you like to schedule? {ServiceCatalog.CatalogSummary}.";
        }

        if (request.RequestedDate == default || !request.HasTime)
        {
            return $"I can schedule your {request.ServiceName}. What day and time works best?";
        }

        return $"Your {request.ServiceName} for {request.RequestedDate:g} Pacific is saved. Reply YES to confirm and put it on {ReignContact.ProviderFirstName}'s schedule.";
    }

    private static DateTime? TryParseDay(string text, DateTime today)
    {
        if (text.Contains("tomorrow"))
        {
            return today.AddDays(1);
        }

        if (text.Contains("today") || text.Contains("tonight"))
        {
            return today;
        }

        var nextWeekday = Regex.Match(text, @"\bnext\s+(sunday|monday|tuesday|wednesday|thursday|friday|saturday)\b");
        if (nextWeekday.Success)
        {
            return NextWeekday(today, nextWeekday.Groups[1].Value, requireFutureWeek: true);
        }

        var weekday = Regex.Match(text, @"\b(sunday|monday|tuesday|wednesday|thursday|friday|saturday)\b");
        if (weekday.Success)
        {
            return NextWeekday(today, weekday.Groups[1].Value, requireFutureWeek: false);
        }

        var numeric = Regex.Match(text, @"\b(\d{1,2})/(\d{1,2})(?:/(\d{2,4}))?\b");
        if (numeric.Success &&
            int.TryParse(numeric.Groups[1].Value, out var month) &&
            int.TryParse(numeric.Groups[2].Value, out var day))
        {
            var year = today.Year;
            if (numeric.Groups[3].Success && int.TryParse(numeric.Groups[3].Value, out var parsedYear))
            {
                year = parsedYear < 100 ? 2000 + parsedYear : parsedYear;
            }

            if (TryMakeDate(year, month, day, today, out var fromNumeric))
            {
                return fromNumeric;
            }
        }

        var named = Regex.Match(
            text,
            @"\b(january|february|march|april|may|june|july|august|september|october|november|december|jan|feb|mar|apr|jun|jul|aug|sep|sept|oct|nov|dec)\s+(\d{1,2})(?:st|nd|rd|th)?(?:,?\s*(\d{4}))?\b");
        if (named.Success)
        {
            var monthNumber = MonthNumber(named.Groups[1].Value);
            if (monthNumber != 0 &&
                int.TryParse(named.Groups[2].Value, out var namedDay))
            {
                var year = today.Year;
                if (named.Groups[3].Success && int.TryParse(named.Groups[3].Value, out var namedYear))
                {
                    year = namedYear;
                }

                if (TryMakeDate(year, monthNumber, namedDay, today, out var fromNamed))
                {
                    return fromNamed;
                }
            }
        }

        return null;
    }

    private static bool TryParseTime(string text, out int hour, out int minute)
    {
        hour = 0;
        minute = 0;

        var timeMatch = Regex.Match(
            text,
            @"(\d{1,2})(:(\d{2}))?\s*(am|pm|a\.m\.|p\.m\.)");

        if (timeMatch.Success)
        {
            hour = int.Parse(timeMatch.Groups[1].Value, CultureInfo.InvariantCulture);
            minute = timeMatch.Groups[3].Success
                ? int.Parse(timeMatch.Groups[3].Value, CultureInfo.InvariantCulture)
                : 0;
            var modifier = timeMatch.Groups[4].Value.Replace(".", "", StringComparison.Ordinal);

            if (modifier == "pm" && hour < 12)
            {
                hour += 12;
            }

            if (modifier == "am" && hour == 12)
            {
                hour = 0;
            }

            return hour is >= 0 and <= 23 && minute is >= 0 and <= 59;
        }

        var twentyFour = Regex.Match(text, @"\b([01]?\d|2[0-3]):([0-5]\d)\b");
        if (twentyFour.Success)
        {
            hour = int.Parse(twentyFour.Groups[1].Value, CultureInfo.InvariantCulture);
            minute = int.Parse(twentyFour.Groups[2].Value, CultureInfo.InvariantCulture);
            return true;
        }

        return false;
    }

    private static DateTime NextWeekday(DateTime today, string weekday, bool requireFutureWeek)
    {
        var target = Array.IndexOf(Weekdays, weekday);
        var delta = (target - (int)today.DayOfWeek + 7) % 7;
        if (delta == 0 && requireFutureWeek)
        {
            delta = 7;
        }

        return today.AddDays(delta);
    }

    private static int MonthNumber(string token) => token switch
    {
        "january" or "jan" => 1,
        "february" or "feb" => 2,
        "march" or "mar" => 3,
        "april" or "apr" => 4,
        "may" => 5,
        "june" or "jun" => 6,
        "july" or "jul" => 7,
        "august" or "aug" => 8,
        "september" or "sep" or "sept" => 9,
        "october" or "oct" => 10,
        "november" or "nov" => 11,
        "december" or "dec" => 12,
        _ => 0
    };

    private static bool TryMakeDate(int year, int month, int day, DateTime today, out DateTime date)
    {
        date = default;
        try
        {
            date = new DateTime(year, month, day, 0, 0, 0, DateTimeKind.Unspecified);
            if (date < today && year == today.Year)
            {
                date = date.AddYears(1);
            }

            return true;
        }
        catch
        {
            return false;
        }
    }
}
