namespace REIGN.Data;

/// <summary>
/// Appointment times are Pacific wall-clock values with <see cref="DateTimeKind.Unspecified"/>.
/// Npgsql 6+ maps <see cref="DateTime"/> to <c>timestamp with time zone</c> and rejects Unspecified
/// writes, which 500s live booking on Postgres. Keep wall-clock columns as timestamp without time zone
/// and enable Npgsql's legacy DateTime behavior before any connection is opened.
/// </summary>
public static class PostgresTimestamps
{
    public const string LegacySwitch = "Npgsql.EnableLegacyTimestampBehavior";

    public const string WallClockColumnType = "timestamp without time zone";

    public static void EnableLegacyBehavior() =>
        AppContext.SetSwitch(LegacySwitch, true);

    public static bool LegacyBehaviorEnabled =>
        AppContext.TryGetSwitch(LegacySwitch, out var enabled) && enabled;
}
