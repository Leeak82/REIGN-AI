namespace REIGN.Core.AI;

public class ReignAssistant : IReignAssistant
{
    public string GenerateResponse(string message)
    {
        var text = message.ToLower();

        if (text.Contains("hour") || text.Contains("hr"))
            return "An hour visit is $500. What day and time would you like?";

        if (text.Contains("half") || text.Contains("hh") || text.Contains("30"))
            return "A half hour visit is $300. What day and time works best?";

        if (text.Contains("quick") || text.Contains("qv"))
            return "A quick visit is $150. What day and time would you like?";

        if (text.Contains("today") || text.Contains("tonight") || text.Contains("same"))
            return "Same-day appointments are available with at least one hour notice. What time would you like?";

        return "Hi, this is REIGN AI. I can help schedule a Quick Visit ($150), Half Hour ($300), or Hour visit ($500). What would you like to book?";
    }
}