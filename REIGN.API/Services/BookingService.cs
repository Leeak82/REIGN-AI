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



            // QV — Quick Visit
            if ((text.Contains("quick") ||
                 text.Contains("qv")) &&
                 name.Contains("qv"))
            {
                request.ServiceName = service.Name;
                break;
            }



            // HH — Half Hour
            if ((text.Contains("half") ||
                 text.Contains("half hour") ||
                 text.Contains("30") ||
                 text.Contains("30 min") ||
                 text.Contains("thirty")) &&
                 name.Contains("hh"))
            {
                request.ServiceName = service.Name;
                break;
            }



            // HR — One Hour
            if ((text.Contains("one hour") ||
                 text.Contains("hour") ||
                 text.Contains("60") ||
                 text.Contains("60 min") ||
                 text.Contains("full hour")) &&
                 name.Contains("hr"))
            {
                request.ServiceName = service.Name;
                break;
            }
        }




        DateTime baseDate = DateTime.Today;



        if(text.Contains("tomorrow"))
        {
            baseDate = DateTime.Today.AddDays(1);
        }




        var timeMatch = Regex.Match(
            text,
            @"(\d{1,2})(:(\d{2}))?\s*(am|pm)"
        );



        if(timeMatch.Success)
        {
            var hour =
                int.Parse(
                    timeMatch.Groups[1].Value);



            var minute = 0;



            if(timeMatch.Groups[3].Success)
            {
                minute =
                    int.Parse(
                        timeMatch.Groups[3].Value);
            }



            var modifier =
                timeMatch.Groups[4].Value;



            if(modifier == "pm" && hour < 12)
                hour += 12;



            if(modifier == "am" && hour == 12)
                hour = 0;



            request.RequestedDate =
                baseDate
                .AddHours(hour)
                .AddMinutes(minute);
        }



        return request;
    }
}
