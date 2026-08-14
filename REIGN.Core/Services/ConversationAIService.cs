using REIGN.Core.Catalog;

namespace REIGN.Core.Services;

public class ConversationAIService
{
    public string ProcessMessage(string message)
    {
        message = message.ToLowerInvariant();

        if (message.Contains("hours") || message.Contains("open") ||
            (message.Contains("when") && message.Contains("available")))
        {
            return "Appointments are typically 9am–5pm. Same-day visits need at least one hour notice. What would you like to book?";
        }

        if (message.Contains("qv") || message.Contains("quick"))
            return $"A Quick Visit (QV) is ${ServiceCatalog.QuickVisitPrice:0} and takes less than 30 minutes. What day and time would you like?";

        if (message.Contains("hh") || message.Contains("half") || message.Contains("30 min"))
            return $"A Half Hour appointment (HH) is ${ServiceCatalog.HalfHourPrice:0} for {ServiceCatalog.HalfHourMinutes} minutes. What day and time works best?";

        if (message.Contains("hr") || message.Contains("hour"))
            return $"An Hour appointment (HR) is ${ServiceCatalog.HourPrice:0} for {ServiceCatalog.HourMinutes} minutes. What day and time would you like to schedule?";

        if (message.Contains("how much") || message.Contains("price") || message.Contains("cost") || message.Contains("services"))
            return $"Hi, this is Miss Reign. {ServiceCatalog.CatalogSummary}. What would you like to book?";

        return $"Hi, this is Miss Reign. I can help schedule your appointment. {ServiceCatalog.CatalogSummary}. What would you like to book?";
    }
}
