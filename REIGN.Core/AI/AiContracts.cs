namespace REIGN.Core.AI;

public enum ReignIntentKind
{
    Unknown = 0,
    Greeting = 1,
    NameCapture = 2,
    BusinessQuestion = 3,
    Schedule = 4,
    Confirm = 5,
    Cancel = 6,
    OwnerActivity = 7
}

public class DetectedIntent
{
    public ReignIntentKind Kind { get; set; }

    public string Label { get; set; } = "unknown";

    public string? ServiceName { get; set; }

    public double Confidence { get; set; }
}

public class AiMessage
{
    public string Role { get; set; } = "user";

    public string Content { get; set; } = "";
}

public class AiCompletionRequest
{
    public string UserMessage { get; set; } = "";

    public string BusinessProfile { get; set; } = "";

    public string MemoryContext { get; set; } = "";

    public string ConversationState { get; set; } = "";

    public string Intent { get; set; } = "";

    public List<AiMessage> RecentMessages { get; set; } = [];
}

public class AiCompletionResult
{
    public string Text { get; set; } = "";

    public string Provider { get; set; } = "";

    public bool UsedLiveModel { get; set; }

    public bool FellBack { get; set; }

    public string? Error { get; set; }
}

public interface IAiProvider
{
    string ProviderName { get; }

    bool IsConfigured { get; }

    Task<AiCompletionResult> CompleteAsync(AiCompletionRequest request, CancellationToken cancellationToken = default);
}
