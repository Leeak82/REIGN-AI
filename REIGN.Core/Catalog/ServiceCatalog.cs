namespace REIGN.Core.Catalog;

/// <summary>
/// Canonical REIGN service catalog. QV / HH / HR only — not automotive.
/// </summary>
public static class ServiceCatalog
{
    public const string QuickVisitCode = "QV";
    public const string QuickVisitName = "Quick Visit";
    public const decimal QuickVisitPrice = 150m;
    public const int QuickVisitMinutes = 20;

    public const string HalfHourCode = "HH";
    public const string HalfHourName = "Half Hour";
    public const decimal HalfHourPrice = 300m;
    public const int HalfHourMinutes = 30;

    public const string HourCode = "HR";
    public const string HourName = "Hour";
    public const decimal HourPrice = 500m;
    public const int HourMinutes = 60;

    public static readonly Guid QuickVisitId = Guid.Parse("9c1a1111-1111-4111-8111-111111111111");
    public static readonly Guid HalfHourId = Guid.Parse("9c1a2222-2222-4222-8222-222222222222");
    public static readonly Guid HourId = Guid.Parse("9c1a3333-3333-4333-8333-333333333333");

    public static readonly Guid QuickVisitRecommendationId = Guid.Parse("9c1aaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa");
    public static readonly Guid HalfHourRecommendationId = Guid.Parse("9c1bbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb");
    public static readonly Guid HourRecommendationId = Guid.Parse("9c1ccccc-cccc-4ccc-8ccc-cccccccccccc");

    public static string CatalogSummary =>
        $"{QuickVisitCode} {QuickVisitName} (${QuickVisitPrice:0}, under 30 minutes), " +
        $"{HalfHourCode} {HalfHourName} (${HalfHourPrice:0}, {HalfHourMinutes} minutes), " +
        $"{HourCode} {HourName} (${HourPrice:0}, {HourMinutes} minutes)";
}
