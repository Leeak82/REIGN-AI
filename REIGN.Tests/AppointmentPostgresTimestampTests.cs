using Microsoft.EntityFrameworkCore;
using REIGN.Data;
using REIGN.Data.Models;
using REIGN.Data.Schema;
using Xunit;

namespace REIGN.Tests;

public class AppointmentPostgresTimestampTests
{
    [Fact]
    public void Legacy_timestamp_behavior_can_be_enabled_before_npgsql_starts()
    {
        PostgresTimestamps.EnableLegacyBehavior();
        Assert.True(PostgresTimestamps.LegacyBehaviorEnabled);
        Assert.Equal("timestamp without time zone", PostgresTimestamps.WallClockColumnType);
    }

    [Fact]
    public void Postgres_model_stores_appointment_time_as_wall_clock_timestamp()
    {
        PostgresTimestamps.EnableLegacyBehavior();
        var options = new DbContextOptionsBuilder<ReignDbContext>()
            .UseNpgsql("Host=localhost;Database=reign;Username=postgres;Password=postgres")
            .Options;
        using var db = new ReignDbContext(options);

        var columnType = db.Model
            .FindEntityType(typeof(Appointment))
            ?.FindProperty(nameof(Appointment.AppointmentTime))
            ?.GetColumnType();
        Assert.Equal(PostgresTimestamps.WallClockColumnType, columnType);

        var script = db.Database.GenerateCreateScript();
        Assert.Contains("\"AppointmentTime\" timestamp without time zone", script, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "\"AppointmentTime\" timestamp with time zone",
            script,
            StringComparison.Ordinal);
        Assert.Contains("timestamp with time zone", PostgresModel.AppointmentTimeWallClockUpgradeSql, StringComparison.Ordinal);
        Assert.Contains("timestamp without time zone", PostgresModel.AppointmentTimeWallClockUpgradeSql, StringComparison.Ordinal);
        Assert.Contains("AT TIME ZONE 'UTC'", PostgresModel.AppointmentTimeWallClockUpgradeSql, StringComparison.Ordinal);
    }

    [Fact]
    public void Sqlite_model_does_not_force_postgres_timestamp_column_type()
    {
        var options = new DbContextOptionsBuilder<ReignDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;
        using var db = new ReignDbContext(options);
        var columnType = db.Model
            .FindEntityType(typeof(Appointment))
            ?.FindProperty(nameof(Appointment.AppointmentTime))
            ?.GetColumnType();
        Assert.NotEqual(PostgresTimestamps.WallClockColumnType, columnType);
    }
}
