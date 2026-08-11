namespace REIGN.Core.Services;

public class ConversationAIService
{
    public string ProcessMessage(string message)
    {
        message = message.ToLower();

        if (message.Contains("quick"))
            return "A quick visit is $200. What day and time would you like?";

        if (message.Contains("half") || message.Contains("30"))
            return "A half hour appointment is $300. What day and time works best?";

        if (message.Contains("hour"))
            return "An hour appointment is $500. What day and time would you like to schedule?";

        return "Hi, this is Miss Reign. I can help schedule your appointment. Quick visit ($200), half hour ($300), or hour ($500). What would you like to book?";
    }
}