using REIGN.Core.AI;
using REIGN.Core.Catalog;
using REIGN.Core.Services;

namespace REIGN.API.AI;

public class FallbackAiProvider : IAiProvider
{
    private readonly ConversationAIService _conversationAi;
    private readonly IReignAssistant _assistant;

    public FallbackAiProvider(ConversationAIService conversationAi, IReignAssistant assistant)
    {
        _conversationAi = conversationAi;
        _assistant = assistant;
    }

    public string ProviderName => "Fallback";

    public bool IsConfigured => true;

    public Task<AiCompletionResult> CompleteAsync(AiCompletionRequest request, CancellationToken cancellationToken = default)
    {
        string text;
        if (request.Intent.Equals("owner_activity", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(request.MemoryContext))
        {
            text = request.MemoryContext;
        }
        else if (!string.IsNullOrWhiteSpace(request.MemoryContext) &&
                 request.MemoryContext.StartsWith("Returning customer", StringComparison.OrdinalIgnoreCase))
        {
            text = BuildReturningReply(request);
        }
        else
        {
            text = _conversationAi.ProcessMessage(request.UserMessage);
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            text = _assistant.GenerateResponse(request.UserMessage);
        }

        return Task.FromResult(new AiCompletionResult
        {
            Provider = ProviderName,
            Text = text,
            FellBack = true,
            UsedLiveModel = false
        });
    }

    private static string BuildReturningReply(AiCompletionRequest request)
    {
        var name = ReadReturningName(request.MemoryContext);
        var greeting = string.IsNullOrWhiteSpace(name)
            ? "Welcome back."
            : $"Welcome back, {name}.";
        return
            $"{greeting} I can help with {ServiceCatalog.CatalogSummary}. " +
            "Would you like to book QV, HH, or HR?";
    }

    private static string? ReadReturningName(string memory)
    {
        const string prefix = "Returning customer: ";
        if (!memory.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var rest = memory[prefix.Length..];
        var end = rest.IndexOf('.');
        var name = (end >= 0 ? rest[..end] : rest).Trim();
        if (string.IsNullOrWhiteSpace(name) || name.StartsWith('+') || name.Any(char.IsDigit))
        {
            return null;
        }

        return name;
    }
}
