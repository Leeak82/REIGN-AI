using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using REIGN.Core.Catalog;
using REIGN.Core.Models;
using REIGN.Data;

namespace REIGN.API.Services;

public class BookingService
{
    private readonly ReignDbContext _db;

    public BookingService(ReignDbContext db)
    {
        _db = db;
    }

    public async Task<AppointmentRequest> ParseRequest(string message)
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

        DateTime baseDate = DateTime.Today;
        if (text.Contains("tomorrow"))
        {
            baseDate = DateTime.Today.AddDays(1);
        }
        else if (text.Contains("today") || text.Contains("tonight"))
        {
            baseDate = DateTime.Today;
        }

        var timeMatch = Regex.Match(
            text,
            @"(\d{1,2})(:(\d{2}))?\s*(am|pm|a\.m\.|p\.m\.)");

        if (timeMatch.Success)
        {
            var hour = int.Parse(timeMatch.Groups[1].Value);
            var minute = timeMatch.Groups[3].Success ? int.Parse(timeMatch.Groups[3].Value) : 0;
            var modifier = timeMatch.Groups[4].Value.Replace(".", "");

            if (modifier == "pm" && hour < 12)
                hour += 12;

            if (modifier == "am" && hour == 12)
                hour = 0;

            request.RequestedDate = baseDate.AddHours(hour).AddMinutes(minute);
        }
        else
        {
            var twentyFour = Regex.Match(text, @"\b([01]?\d|2[0-3]):([0-5]\d)\b");
            if (twentyFour.Success)
            {
                request.RequestedDate = baseDate
                    .AddHours(int.Parse(twentyFour.Groups[1].Value))
                    .AddMinutes(int.Parse(twentyFour.Groups[2].Value));
            }
            else if (text.Contains("today") || text.Contains("tonight"))
            {
                request.RequestedDate = DateTime.Today.AddHours(18);
            }
            else if (text.Contains("tomorrow"))
            {
                request.RequestedDate = DateTime.Today.AddDays(1).AddHours(12);
            }
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

        if (request.RequestedDate == default)
        {
            return $"I can schedule your {request.ServiceName}. What day and time works best?";
        }

        return $"Your {request.ServiceName} appointment request for {request.RequestedDate:g} has been received. Reply YES to confirm.";
    }
}
