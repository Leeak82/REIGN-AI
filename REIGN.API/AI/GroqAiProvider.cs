using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using REIGN.API.Options;
using REIGN.API.Services;
using REIGN.Core.AI;
using REIGN.Core.Catalog;

namespace REIGN.API.AI;

public class GroqAiProvider : IAiProvider
{
    private readonly HttpClient _http;
    private readonly AiOptions _options;
    private readonly IBusinessProfileAccessor _business;
    private readonly ILogger<GroqAiProvider> _logger;

    public GroqAiProvider(
        HttpClient http,
        IOptions<AiOptions> options,
        IBusinessProfileAccessor business,
        ILogger<GroqAiProvider> logger)
    {
        _http = http;
        _options = options.Value;
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            _options.ApiKey = Environment.GetEnvironmentVariable("GROQ_API_KEY")
                ?? Environment.GetEnvironmentVariable("Ai__ApiKey")
                ?? "";
        }
        _business = business;
        _logger = logger;
        if (_http.BaseAddress == null && Uri.TryCreate(_options.BaseUrl, UriKind.Absolute, out var baseUri))
        {
            _http.BaseAddress = baseUri;
        }

        _http.Timeout = TimeSpan.FromSeconds(Math.Clamp(_options.TimeoutSeconds, 5, 60));
    }

    public string ProviderName => "Groq";

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_options.ApiKey);

    public async Task<AiCompletionResult> CompleteAsync(AiCompletionRequest request, CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            return new AiCompletionResult
            {
                Provider = ProviderName,
                FellBack = true,
                Error = "Groq API key is not configured."
            };
        }

        var payload = new
        {
            model = _options.Model,
            temperature = 0.3,
            max_tokens = _options.MaxTokens,
            messages = await BuildMessages(request, cancellationToken)
        };

        using var message = new HttpRequestMessage(HttpMethod.Post, "chat/completions");
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
        message.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        try
        {
            using var response = await _http.SendAsync(message, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Groq completion failed: {Status} {Body}", (int)response.StatusCode, Truncate(body));
                return new AiCompletionResult
                {
                    Provider = ProviderName,
                    FellBack = true,
                    Error = $"Groq HTTP {(int)response.StatusCode}"
                };
            }

            using var doc = JsonDocument.Parse(body);
            var text = doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString()
                ?.Trim();

            if (string.IsNullOrWhiteSpace(text))
            {
                return new AiCompletionResult
                {
                    Provider = ProviderName,
                    FellBack = true,
                    Error = "Groq returned an empty reply."
                };
            }

            return new AiCompletionResult
            {
                Provider = ProviderName,
                Text = text,
                UsedLiveModel = true
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Groq completion threw");
            return new AiCompletionResult
            {
                Provider = ProviderName,
                FellBack = true,
                Error = ex.Message
            };
        }
    }

    private async Task<List<object>> BuildMessages(AiCompletionRequest request, CancellationToken cancellationToken)
    {
        var profile = await _business.GetActiveAsync(cancellationToken);
        var system =
            $"You are {profile.AssistantName}, the AI assistant for {profile.Name}. " +
            $"{profile.Offering} Hours: {profile.Hours} Tone: {profile.Tone} " +
            $"Services: {ServiceCatalog.CatalogSummary}. " +
            "Never invent prices or services. Never use automotive or mechanic language. " +
            "If the customer wants to book, collect service (QV/HH/HR) and day/time, then ask them to reply YES to confirm so Jessica can see it on her schedule. " +
            "Use customer memory when present. Keep replies short for SMS.";

        var messages = new List<object>
        {
            new { role = "system", content = system }
        };

        if (!string.IsNullOrWhiteSpace(request.MemoryContext) ||
            !string.IsNullOrWhiteSpace(request.ConversationState) ||
            !string.IsNullOrWhiteSpace(request.Intent))
        {
            messages.Add(new
            {
                role = "system",
                content =
                    $"Intent: {request.Intent}. State: {request.ConversationState}. Memory: {request.MemoryContext}"
            });
        }

        foreach (var prior in request.RecentMessages.TakeLast(8))
        {
            var role = prior.Role.Equals("Assistant", StringComparison.OrdinalIgnoreCase) ||
                       prior.Role.Equals("system", StringComparison.OrdinalIgnoreCase)
                ? "assistant"
                : "user";
            messages.Add(new { role, content = prior.Content });
        }

        messages.Add(new { role = "user", content = request.UserMessage });
        return messages;
    }

    private static string Truncate(string value) =>
        value.Length <= 300 ? value : value[..300];
}
