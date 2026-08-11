namespace REIGN.Core.AI;

public class ReignAssistant : IReignAssistant
{
    public string GenerateResponse(string message)
    {
        var text = message.ToLower();

        if (text.Contains("hour"))
            return "An hour appointment is $500. What day and time would you like?";

        if (text.Contains("half") || text.Contains("30"))
            return "A half hour appointment is $300. What day and time works best?";

        if (text.Contains("quick"))
            return "A quick visit is $200. What day and time would you like?";

        if (text.Contains("today") || text.Contains("tonight") || text.Contains("same"))
            return "Same-day appointments are available with at least one hour notice. What time would you like?";

        return "Hi, this is Miss Reign. I can help schedule quick visits, half hour, or hour appointments. What service are you interested in?";
    }
}