using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using REIGN.API.Calendar;
using REIGN.API.Controllers;
using REIGN.API.Messaging;
using REIGN.API.Services;
using REIGN.Core.Catalog;
using REIGN.Data.Models;
using Xunit;

namespace REIGN.Tests;

public class ServiceManagementAndLanguageTests
{
    [Fact]
    public async Task Booking_understands_today_at_bare_hour()
    {
        await using var harness = await IncomingSmsProcessorTests.Harness.CreateAsync();
        var booking = new BookingService(harness.Db);

        var parsed = await booking.ParseRequest("HH today at 4?");

        var clock = new BusinessClock();
        Assert.Equal(ServiceCatalog.HalfHourName, parsed.ServiceName);
        Assert.True(parsed.HasTime);
        Assert.Equal(16, parsed.RequestedDate.Hour);
        Assert.Equal(clock.Today.Month, parsed.RequestedDate.Month);
    }

    [Fact]
    public async Task Booking_remembers_day_when_customer_says_i_said_4()
    {
        await using var harness = await IncomingSmsProcessorTests.Harness.CreateAsync();

        await harness.Processor.ProcessAsync(new IncomingSmsMessage
        {
            From = "3605550710",
            Body = "Hello"
        }, sendReplyViaProvider: false);

        await harness.Processor.ProcessAsync(new IncomingSmsMessage
        {
            From = "3605550710",
            Body = "Bruce"
        }, sendReplyViaProvider: false);

        var day = await harness.Processor.ProcessAsync(new IncomingSmsMessage
        {
            From = "3605550710",
            Body = "HH tomorrow"
        }, sendReplyViaProvider: false);

        Assert.Contains("time", day.Reply, StringComparison.OrdinalIgnoreCase);

        var timed = await harness.Processor.ProcessAsync(new IncomingSmsMessage
        {
            From = "3605550710",
            Body = "I said 4"
        }, sendReplyViaProvider: false);

        Assert.Contains("YES", timed.Reply, StringComparison.OrdinalIgnoreCase);
        var appointment = Assert.Single(harness.Db.Appointments.Include(x => x.Service));
        Assert.Equal(ServiceCatalog.HalfHourName, appointment.Service.Name);
        Assert.Equal(16, appointment.AppointmentTime.Hour);
        var clock = new BusinessClock();
        Assert.Equal(clock.Today.AddDays(1).Date, appointment.AppointmentTime.Date);
    }

    [Fact]
    public async Task Newly_configured_service_can_be_selected_then_timed()
    {
        await using var harness = await IncomingSmsProcessorTests.Harness.CreateAsync();
        harness.Db.Services.Add(new Service
        {
            Name = "Deluxe Session",
            Price = 425m,
            DurationMinutes = 45,
            Active = true
        });
        await harness.Db.SaveChangesAsync();

        await harness.Processor.ProcessAsync(new IncomingSmsMessage
        {
            From = "3605550711",
            Body = "Hello"
        }, sendReplyViaProvider: false);
        await harness.Processor.ProcessAsync(new IncomingSmsMessage
        {
            From = "3605550711",
            Body = "Taylor"
        }, sendReplyViaProvider: false);

        var chose = await harness.Processor.ProcessAsync(new IncomingSmsMessage
        {
            From = "3605550711",
            Body = "Deluxe Session"
        }, sendReplyViaProvider: false);
        Assert.Contains("time", chose.Reply, StringComparison.OrdinalIgnoreCase);

        var booked = await harness.Processor.ProcessAsync(new IncomingSmsMessage
        {
            From = "3605550711",
            Body = "tomorrow at 4"
        }, sendReplyViaProvider: false);

        Assert.Contains("YES", booked.Reply, StringComparison.OrdinalIgnoreCase);
        var appointment = Assert.Single(harness.Db.Appointments.Include(x => x.Service));
        Assert.Equal("Deluxe Session", appointment.Service.Name);
        Assert.Equal(425m, appointment.Price);
        Assert.Equal(45, appointment.DurationMinutes);
        Assert.Equal(16, appointment.AppointmentTime.Hour);
    }

    [Fact]
    public async Task Service_management_requires_admin_key_and_updates_live_catalog()
    {
        await using var harness = await IncomingSmsProcessorTests.Harness.CreateAsync();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["REIGN_ADMIN_API_KEY"] = "test-admin-key"
            })
            .Build();
        var catalog = new CatalogIntelligence(harness.Db);
        var controller = new ServiceCatalogController(
            catalog,
            harness.Db,
            config,
            NullLogger<ServiceCatalogController>.Instance);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };

        var denied = await controller.Manage();
        Assert.IsType<UnauthorizedObjectResult>(denied);

        controller.Request.Headers["X-REIGN-ADMIN-KEY"] = "test-admin-key";
        var created = await controller.Create(new ServiceEditRequest
        {
            Name = "Late Visit",
            Price = 225m,
            DurationMinutes = 25,
            Active = true
        });
        Assert.IsType<OkObjectResult>(created);

        var row = await harness.Db.Services.SingleAsync(x => x.Name == "Late Visit");
        var updated = await controller.Update(row.Id, new ServiceEditRequest
        {
            Name = "Late Visit",
            Price = 250m,
            DurationMinutes = 30,
            Active = false
        });
        Assert.IsType<OkObjectResult>(updated);

        row = await harness.Db.Services.SingleAsync(x => x.Id == row.Id);
        Assert.Equal(250m, row.Price);
        Assert.Equal(30, row.DurationMinutes);
        Assert.False(row.Active);
        Assert.DoesNotContain("Late Visit", await catalog.GetSummaryAsync(), StringComparison.OrdinalIgnoreCase);
    }
}
