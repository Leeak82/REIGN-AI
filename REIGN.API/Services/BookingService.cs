using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
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
        var text = message.ToLower();

        var request = new AppointmentRequest();

        var services = await _db.Services
            .Where(x => x.Active)
            .ToListAsync();

        foreach (var service in services)
        {
            var name = service.Name.ToLower();

            if (text.Contains("oil") && name.Contains("oil"))
            {
                request.ServiceName = service.Name;
                break;
            }

            if (text.Contains("brake") && name.Contains("brake"))
            {
                request.ServiceName = service.Name;
                break;
            }

            if ((text.Contains("diagnostic") || text.Contains("check engine"))
                && name.Contains("diagnostic"))
            {
                request.ServiceName = service.Name;
                break;
            }

            if (text.Contains("inspection") && name.Contains("inspection"))
            {
                request.ServiceName = service.Name;
                break;
            }
        }


        DateTime baseDate = DateTime.Today;


        if (text.Contains("today"))
        {
            baseDate = DateTime.Today;
        }
        else if (text.Contains("tomorrow"))
        {
            baseDate = DateTime.Today.AddDays(1);
        }
        else
        {
            baseDate = DateTime.Today;
        }


        // Extract time like:
        // 10 AM
        // 10:30 AM
        // 2 PM
        // 14:00

        var timeMatch = Regex.Match(
            text,
            @"(\d{1,2})(:(\d{2}))?\s*(am|pm|a\.m\.|p\.m\.)"
        );


        if (timeMatch.Success)
        {
            var hour = int.Parse(timeMatch.Groups[1].Value);

            var minute = 0;

            if (timeMatch.Groups[3].Success)
            {
                minute = int.Parse(timeMatch.Groups[3].Value);
            }


            var modifier = timeMatch.Groups[4].Value.Replace(".", "");


            if (modifier == "pm" && hour < 12)
                hour += 12;

            if (modifier == "am" && hour == 12)
                hour = 0;


            request.RequestedDate =
                baseDate
                .AddHours(hour)
                .AddMinutes(minute);
        }
        else if (text.Contains("today"))
        {
            request.RequestedDate =
                DateTime.Today.AddHours(18);
        }
        else if (text.Contains("tomorrow"))
        {
            request.RequestedDate =
                DateTime.Today.AddDays(1).AddHours(12);
        }


        return request;
    }


    public string CreateBooking(AppointmentRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ServiceName))
        {
            return "What service would you like to schedule?";
        }

        if (request.RequestedDate == default)
        {
            return $"I can schedule your {request.ServiceName}. What day and time works best?";
        }

        return $"Your {request.ServiceName} appointment request for {request.RequestedDate:g} has been received. Reply YES to confirm.";
    }
}