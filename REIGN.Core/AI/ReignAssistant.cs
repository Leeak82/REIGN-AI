using REIGN.Core.Catalog;

namespace REIGN.Core.AI;

public class ReignAssistant : IReignAssistant
{
    public string GenerateResponse(string message)
    {
        var text = message.ToLowerInvariant();

        if (text.Contains("qv") || text.Contains("quick"))
            return $"A Quick Visit (QV) is ${ServiceCatalog.QuickVisitPrice:0} and takes less than 30 minutes. What day and time would you like?";

        if (text.Contains("hh") || text.Contains("half") || text.Contains("30"))
            return $"A Half Hour appointment (HH) is ${ServiceCatalog.HalfHourPrice:0} for {ServiceCatalog.HalfHourMinutes} minutes. What day and time works best?";

        if (text.Contains("hr") || text.Contains("hour"))
            return $"An Hour appointment (HR) is ${ServiceCatalog.HourPrice:0} for {ServiceCatalog.HourMinutes} minutes. What day and time would you like?";

        if (text.Contains("today") || text.Contains("tonight") || text.Contains("same"))
            return "Same-day appointments are available with at least one hour notice. What time would you like?";

        return $"Hi, this is Miss Reign. I can help schedule {ServiceCatalog.CatalogSummary}. What service are you interested in?";
    }
}
