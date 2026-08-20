using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using REIGN.API.Calendar;
using REIGN.API.Services;
using REIGN.Core.Catalog;
using REIGN.Data;
using REIGN.Data.Models;
using REIGN.Data.Schema;
using REIGN.Data.Seed;
using Xunit;

namespace REIGN.Tests;

public class AppointmentCalendarSyncTests
{
    [Fact]
    public async Task Failed_google_sync_does_not_store_an_event_id()
    {
        await using var db = await CreateDbAsync();
        var appointment = await SeedAppointmentAsync(db);
        var calendar = new StubCalendar
        {
            Result = CalendarSyncResult.Fail("Google", "Google Calendar HTTP 403", googleStatusCode: 403)
        };
        var sync = new AppointmentCalendarSync(db, calendar, NullLogger<AppointmentCalendarSync>.Instance);

        var result = await sync.SyncAsync(appointment);

        Assert.False(result.Succeeded);
        Assert.Equal(403, result.GoogleStatusCode);
        Assert.True(string.IsNullOrWhiteSpace(appointment.ExternalCalendarEventId));
        Assert.Equal("Pending", appointment.Status);
    }

    [Fact]
    public async Task Successful_google_sync_stores_the_retrievable_event_id()
    {
        await using var db = await CreateDbAsync();
        var appointment = await SeedAppointmentAsync(db);
        var calendar = new StubCalendar
        {
            Result = CalendarSyncResult.Ok(
                "Google",
                "1tfftt6crrdcaju5iimch6r3lc",
                htmlLink: "https://www.google.com/calendar/event?eid=abc",
                timeZone: "America/New_York",
                calendarId: "primary",
                googleStatusCode: 200)
        };
        var sync = new AppointmentCalendarSync(db, calendar, NullLogger<AppointmentCalendarSync>.Instance);

        var result = await sync.SyncAsync(appointment);

        Assert.True(result.Succeeded);
        Assert.Equal("1tfftt6crrdcaju5iimch6r3lc", appointment.ExternalCalendarEventId);
        Assert.Equal("1tfftt6crrdcaju5iimch6r3lc", (await db.Appointments.SingleAsync()).ExternalCalendarEventId);
    }

    private static async Task<ReignDbContext> CreateDbAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ReignDbContext>()
            .UseSqlite(connection)
            .Options;
        var db = new ReignDbContext(options);
        await SqliteSchemaUpgrades.ApplyAsync(db);
        await ServiceCatalogBootstrapper.EnsureAsync(db);
        return db;
    }

    private static async Task<Appointment> SeedAppointmentAsync(ReignDbContext db)
    {
        var customer = new Customer
        {
            Name = "Test",
            PhoneNumber = "+15555550123"
        };
        db.Customers.Add(customer);
        var service = await db.Services.SingleAsync(x => x.Id == ServiceCatalog.QuickVisitId);
        var appointment = new Appointment
        {
            Customer = customer,
            Service = service,
            Price = service.Price,
            DurationMinutes = service.DurationMinutes,
            AppointmentTime = new DateTime(2026, 8, 21, 14, 0, 0, DateTimeKind.Unspecified),
            Status = "Pending",
            CreatedAt = DateTime.UtcNow
        };
        db.Appointments.Add(appointment);
        await db.SaveChangesAsync();
        return appointment;
    }

    private sealed class StubCalendar : ICalendarService
    {
        public CalendarSyncResult Result { get; set; } = CalendarSyncResult.Fail("Google", "unset");

        public string ProviderName => "Google";

        public bool IsConfigured => true;

        public bool IsSimulated => false;

        public bool HasStoredGrant => true;

        public Task<CalendarSyncResult> UpsertAppointmentAsync(CalendarEventRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result);

        public Task<CalendarSyncResult> CancelAppointmentAsync(string? eventId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result);
    }
}
