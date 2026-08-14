using Microsoft.Extensions.Options;
using REIGN.API.Options;
using REIGN.Core.AI;

namespace REIGN.API.AI;

public class ResilientAiProvider : IAiProvider
{
    private readonly GroqAiProvider _groq;
    private readonly FallbackAiProvider _fallback;
    private readonly AiOptions _options;
    private readonly ILogger<ResilientAiProvider> _logger;

    public ResilientAiProvider(
        GroqAiProvider groq,
        FallbackAiProvider fallback,
        IOptions<AiOptions> options,
        ILogger<ResilientAiProvider> logger)
    {
        _groq = groq;
        _fallback = fallback;
        _options = options.Value;
        _logger = logger;
    }

    public string ProviderName => _groq.IsConfigured &&
                                  _options.Provider.Equals("Groq", StringComparison.OrdinalIgnoreCase)
        ? _groq.ProviderName
        : _fallback.ProviderName;

    public bool IsConfigured => _groq.IsConfigured || _fallback.IsConfigured;

    public async Task<AiCompletionResult> CompleteAsync(AiCompletionRequest request, CancellationToken cancellationToken = default)
    {
        var useGroq = _options.Provider.Equals("Groq", StringComparison.OrdinalIgnoreCase) && _groq.IsConfigured;
        if (useGroq)
        {
            var live = await _groq.CompleteAsync(request, cancellationToken);
            if (live.UsedLiveModel && !string.IsNullOrWhiteSpace(live.Text))
            {
                return live;
            }

            _logger.LogWarning("Groq unavailable ({Error}); using fallback assistant.", live.Error);
        }

        var fallback = await _fallback.CompleteAsync(request, cancellationToken);
        fallback.FellBack = true;
        fallback.Error = useGroq ? "Fell back after Groq was unavailable." : fallback.Error;
        return fallback;
    }
}
