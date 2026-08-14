using Microsoft.Extensions.Options;
using REIGN.API.Options;
using REIGN.Core.AI;
using REIGN.Core.Catalog;
using REIGN.Data;
using REIGN.Data.Models;

namespace REIGN.API.Services;

public class ConversationReply
{
    public string Text { get; set; } = "";

    public bool FellBack { get; set; }

    public string Provider { get; set; } = "";
}

public class ConversationEngine
{
    private readonly ReignDbContext _db;
    private readonly IntentDetectionService _intents;
    private readonly ConversationStateService _state;
    private readonly CustomerMemoryService _memory;
    private readonly IntentMemoryService _intentMemory;
    private readonly IAiProvider _ai;
    private readonly BusinessProfileOptions _business;
    private readonly ILogger<ConversationEngine> _logger;

    public ConversationEngine(
        ReignDbContext db,
        IntentDetectionService intents,
        ConversationStateService state,
        CustomerMemoryService memory,
        IntentMemoryService intentMemory,
        IAiProvider ai,
        IOptions<BusinessProfileOptions> business,
        ILogger<ConversationEngine> logger)
    {
        _db = db;
        _intents = intents;
        _state = state;
        _memory = memory;
        _intentMemory = intentMemory;
        _ai = ai;
        _business = business.Value;
        _logger = logger;
    }

    public async Task<string> Process(Customer customer, string message) =>
        (await ProcessDetailed(customer, message, track: true)).Text;

    public async Task<string> Process(Customer customer, string message, bool track) =>
        (await ProcessDetailed(customer, message, track)).Text;

    public async Task<ConversationReply> ProcessDetailed(Customer customer, string message, bool track)
    {
        message = message.Trim();
        var intent = _intents.Detect(message, customer);
        if (track)
        {
            await _state.UpdateAsync(customer, intent, message);
            await _intentMemory.RecordAsync(customer, intent, message);
        }

        if (string.IsNullOrWhiteSpace(customer.Name))
        {
            var extracted = ConversationService.TryExtractName(message);
            if (!string.IsNullOrWhiteSpace(extracted))
            {
                customer.Name = extracted;
                await _db.SaveChangesAsync();
                return new ConversationReply
                {
                    Text = $"Thanks {customer.Name}. I saved your information. {ServiceCatalog.CatalogSummary}. Which would you like?",
                    Provider = "Rules"
                };
            }

            if (intent.Kind is ReignIntentKind.Greeting or ReignIntentKind.Unknown or ReignIntentKind.NameCapture)
            {
                return new ConversationReply
                {
                    Text = "I'd be happy to help. May I get your name first?",
                    Provider = "Rules"
                };
            }
        }

        var memory = await _memory.GetCustomerContext(customer.Id);
        var intentMemory = await _intentMemory.GetAsync(customer.Id);
        var recent = await _memory.GetRecentTurns(customer.Id);

        try
        {
            var completion = await _ai.CompleteAsync(new AiCompletionRequest
            {
                UserMessage = message,
                Intent = intent.Label,
                MemoryContext = string.Join(" ", new[] { memory, intentMemory }.Where(x => !string.IsNullOrWhiteSpace(x))),
                ConversationState = _state.Describe(customer),
                BusinessProfile = $"{_business.Name}. {_business.Offering} Hours: {_business.Hours}",
                RecentMessages = recent.Select(x => new AiMessage { Role = x.Role, Content = x.Content }).ToList()
            });

            if (!string.IsNullOrWhiteSpace(completion.Text))
            {
                if (completion.FellBack)
                {
                    _logger.LogInformation("Assistant used fallback provider for {Phone}", customer.PhoneNumber);
                }

                return new ConversationReply
                {
                    Text = completion.Text,
                    FellBack = completion.FellBack,
                    Provider = completion.Provider
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AI completion failed for {Phone}", customer.PhoneNumber);
        }

        if (intent.Kind == ReignIntentKind.BusinessQuestion)
        {
            return new ConversationReply
            {
                Text = $"{_business.AssistantName} here. {ServiceCatalog.CatalogSummary}. {_business.Hours}",
                FellBack = true,
                Provider = "Rules"
            };
        }

        var greetingName = string.IsNullOrWhiteSpace(customer.Name) ? "" : $" {customer.Name}";
        return new ConversationReply
        {
            Text = $"Hi{greetingName}, how can I help you today? I can book {ServiceCatalog.CatalogSummary}.",
            FellBack = true,
            Provider = "Rules"
        };
    }
}
