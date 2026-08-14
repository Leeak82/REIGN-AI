using REIGN.API.Services;
using REIGN.Core.AI;
using REIGN.Data.Models;

namespace REIGN.API.Services;

public class IntentDetectionService
{
    public DetectedIntent Detect(string message, Customer? customer = null, bool ownerChannel = false)
    {
        var text = (message ?? "").Trim();
        var lower = text.ToLowerInvariant();

        if (ownerChannel)
        {
            return new DetectedIntent
            {
                Kind = ReignIntentKind.OwnerActivity,
                Label = "owner_activity",
                Confidence = 0.95
            };
        }

        if (text.Equals("YES", StringComparison.OrdinalIgnoreCase) ||
            lower is "confirm" or "confirmed" ||
            lower.Contains("sounds good") ||
            lower.Contains("that works"))
        {
            return new DetectedIntent { Kind = ReignIntentKind.Confirm, Label = "confirm", Confidence = 0.95 };
        }

        if (lower.Contains("cancel") || lower.Contains("nevermind") || lower.Contains("never mind"))
        {
            return new DetectedIntent { Kind = ReignIntentKind.Cancel, Label = "cancel", Confidence = 0.9 };
        }

        if (lower.Contains("reschedule") || lower.Contains("change my") || lower.Contains("move my") ||
            lower.Contains("update my appointment"))
        {
            return new DetectedIntent
            {
                Kind = ReignIntentKind.Schedule,
                Label = "schedule",
                ServiceName = BookingService.MatchCatalogService(lower),
                Confidence = 0.88
            };
        }

        if (LooksLikeBusinessQuestion(lower) &&
            !lower.Contains("book") &&
            !lower.Contains("schedule") &&
            !lower.Contains("appointment"))
        {
            return new DetectedIntent { Kind = ReignIntentKind.BusinessQuestion, Label = "business_question", Confidence = 0.85 };
        }

        var service = BookingService.MatchCatalogService(lower);
        var hasTime = lower.Contains("today") || lower.Contains("tomorrow") ||
                      lower.Contains("tonight") || lower.Contains("am") || lower.Contains("pm") ||
                      System.Text.RegularExpressions.Regex.IsMatch(lower, @"\b\d{1,2}(:\d{2})?\b");

        if (!string.IsNullOrWhiteSpace(service) ||
            lower.Contains("book") || lower.Contains("schedule") || lower.Contains("appointment") ||
            (customer?.ConversationState?.CurrentStep == "AwaitingTime" && hasTime))
        {
            return new DetectedIntent
            {
                Kind = ReignIntentKind.Schedule,
                Label = "schedule",
                ServiceName = service,
                Confidence = string.IsNullOrWhiteSpace(service) ? 0.7 : 0.92
            };
        }

        if (string.IsNullOrWhiteSpace(customer?.Name) && ConversationService.TryExtractName(text) != null)
        {
            return new DetectedIntent { Kind = ReignIntentKind.NameCapture, Label = "name_capture", Confidence = 0.8 };
        }

        if (lower is "hi" or "hello" or "hey" || lower.StartsWith("hi ") || lower.StartsWith("hello"))
        {
            return new DetectedIntent { Kind = ReignIntentKind.Greeting, Label = "greeting", Confidence = 0.75 };
        }

        return new DetectedIntent { Kind = ReignIntentKind.Unknown, Label = "unknown", Confidence = 0.4 };
    }

    private static bool LooksLikeBusinessQuestion(string lower) =>
        lower.Contains("price") || lower.Contains("cost") || lower.Contains("how much") ||
        lower.Contains("hours") || lower.Contains("how long") || lower.Contains("what is qv") ||
        lower.Contains("what's qv") || lower.Contains("services") || lower.Contains("offer") ||
        lower.Contains("qv") && (lower.Contains("what") || lower.Contains("tell")) ||
        lower.Contains("do you") || lower.Contains("can you");
}
