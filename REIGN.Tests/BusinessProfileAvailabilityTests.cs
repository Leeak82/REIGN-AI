using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using REIGN.API.Calendar;
using REIGN.API.Controllers;
using REIGN.API.Messaging;
using REIGN.API.Services;
using REIGN.Data.Models;
using Xunit;

namespace REIGN.Tests;

public class BusinessProfileAvailabilityTests
{
    [Fact]
    public void Business_hours_parser_understands_weekdays_range_and_same_day_notice()
    {
        var hours = BusinessHoursParser.Parse(
            "Monday-Friday 9am-5pm. Same-day visits need at least one hour notice.");

        Assert.Equal(TimeSpan.FromHours(9), hours.OpensAt);
        Assert.Equal(TimeSpan.FromHours(17), hours.ClosesAt);
        Assert.True(hours.IsOpen(DayOfWeek.Monday));
        Assert.True(hours.IsOpen(DayOfWeek.Friday));
        Assert.False(hours.IsOpen(DayOfWeek.Sunday));
        Assert.Equal(60, hours.SameDayNoticeMinutes);
    }

    [Fact]
    public async Task Business_management_requires_admin_key_and_updates_live_hours()
    {
        await using var harness = await IncomingSmsProcessorTests.Harness.CreateAsync();
        var business = await harness.Db.Businesses.FirstOrDefaultAsync();
        if (business == null)
        {
            business = new Business
            {
                Id = Guid.NewGuid(),
                Name = "Test Business",
                OwnerName = "Owner",
                Hours = "9am-5pm",
                TimeZone = "America/Los_Angeles",
                Active = true
            };
            harness.Db.Businesses.Add(business);
            await harness.Db.SaveChangesAsync();
        }

        var profiles = new BusinessProfileService(harness.Db);
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["REIGN_ADMIN_API_KEY"] = "test-admin-key"
            })
            .Build();
        var controller = new BusinessesController(harness.Db, profiles, config)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        var denied = await controller.Manage();
        Assert.IsType<UnauthorizedObjectResult>(denied);

        controller.Request.Headers["X-REIGN-ADMIN-KEY"] = "test-admin-key";
        var updated = await controller.Update(business.Id, new BusinessEditRequest
        {
            Name = "Test Business",
            OwnerName = "Owner",
            Phone = "5550100",
            Email = "owner@example.test",
            Address = "Appointment only",
            Hours = "Monday-Saturday 10am-6pm. Same-day visits need at least one hour notice.",
            TimeZone = "America/Los_Angeles"
        });

        Assert.IsType<OkObjectResult>(updated);
        var liveProfile = await profiles.GetActiveAsync();
        Assert.Contains("10am-6pm", liveProfile.Hours, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Availability_reply_is_grounded_in_live_reign_schedule()
    {
        await using var harness = await IncomingSmsProcessorTests.Harness.CreateAsync();
        var business = await harness.Db.Businesses.FirstOrDefaultAsync();
        if (business == null)
        {
            business = new Business
            {
                Id = Guid.NewGuid(),
                Name = "Test Business",
                Hours = "9am-5pm",
                TimeZone = "America/Los_Angeles",
                Active = true
            };
            harness.Db.Businesses.Add(business);
        }
        else
        {
            business.Hours = "9am-5pm";
            business.Active = true;
        }
        await harness.Db.SaveChangesAsync();

        var booking = new BookingService(
            harness.Db,
            new BusinessClock("America/Los_Angeles"),
            new BusinessProfileService(harness.Db));

        var reply = await booking.GetAvailabilityReplyAsync("Half Hour", daysToCheck: 14);

        Assert.Contains("checked the live REIGN schedule", reply, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("booked for weeks", reply, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Sms_question_about_being_booked_for_weeks_bypasses_ai_guessing()
    {
        await using var harness = await IncomingSmsProcessorTests.Harness.CreateAsync();

        await harness.Processor.ProcessAsync(new IncomingSmsMessage
        {
            From = "3605550788",
            Body = "Hello"
        }, sendReplyViaProvider: false);
        await harness.Processor.ProcessAsync(new IncomingSmsMessage
        {
            From = "3605550788",
            Body = "Jordan"
        }, sendReplyViaProvider: false);

        var result = await harness.Processor.ProcessAsync(new IncomingSmsMessage
        {
            From = "3605550788",
            Body = "Are you booked for weeks?"
        }, sendReplyViaProvider: false);

        Assert.Contains("checked the live REIGN schedule", result.Reply, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("booked for weeks", result.Reply, StringComparison.OrdinalIgnoreCase);
    }
}
