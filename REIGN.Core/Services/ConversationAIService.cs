namespace REIGN.Core.Services;

public class ConversationAIService
{
    public string ProcessMessage(string message)
    {
        message = message.ToLower();

        if (message.Contains("quick") || message.Contains("qv"))
            return "A quick visit is $150. What day and time would you like?";

        if (message.Contains("half") || message.Contains("hh") || message.Contains("30"))
            return "A half hour visit is $300. What day and time works best?";

        if (message.Contains("hour") || message.Contains("hr"))
            return "An hour visit is $500. What day and time would you like to schedule?";

        return "Hi, this is REIGN AI. I can help schedule your appointment. QV ($150), HH ($300), or HR ($500). What would you like to book?";
    }
}