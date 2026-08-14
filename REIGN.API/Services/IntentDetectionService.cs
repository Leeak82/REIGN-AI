namespace REIGN.API.Services;

public enum CustomerIntent
{
    Unknown,
    Greeting,
    ServiceInquiry,
    PricingQuestion,
    BookingRequest,
    Confirmation,
    Cancellation,
    GeneralQuestion
}


public class IntentDetectionService
{
    public CustomerIntent Detect(string message)
    {
        message = message
            .ToLower()
            .Trim();


        if (string.IsNullOrWhiteSpace(message))
            return CustomerIntent.Unknown;



        if (
            message == "hi" ||
            message == "hello" ||
            message == "hey" ||
            message == "hey there" ||
            message == "good morning" ||
            message == "good afternoon" ||
            message == "whats up" ||
            message == "what's up"
        )
        {
            return CustomerIntent.Greeting;
        }



        if (
            message == "yes" ||
            message.Contains("confirm")
        )
        {
            return CustomerIntent.Confirmation;
        }



        if (
            message.Contains("cancel") ||
            message.Contains("reschedule")
        )
        {
            return CustomerIntent.Cancellation;
        }



        if (
            message.Contains("price") ||
            message.Contains("cost") ||
            message.Contains("how much") ||
            message.Contains("$")
        )
        {
            return CustomerIntent.PricingQuestion;
        }



        if (
            message.Contains("book") ||
            message.Contains("schedule") ||
            message.Contains("appointment") ||
            message.Contains("need") ||
            message.Contains("want to")
        )
        {
            return CustomerIntent.BookingRequest;
        }



        if (
            message.Contains("service") ||
            message.Contains("visit") ||
            message.Contains("what do you offer")
        )
        {
            return CustomerIntent.ServiceInquiry;
        }



        return CustomerIntent.GeneralQuestion;
    }
}
