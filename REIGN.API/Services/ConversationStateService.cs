using Microsoft.EntityFrameworkCore;
using REIGN.Core.AI;
using REIGN.Data;
using REIGN.Data.Models;

namespace REIGN.API.Services;

public class ConversationStateService
{
    private readonly ReignDbContext _db;

    public ConversationStateService(ReignDbContext db)
    {
        _db = db;
    }

    public async Task<ConversationState> GetOrCreate(Guid customerId)
    {
        var state = await _db.ConversationStates
            .FirstOrDefaultAsync(x => x.CustomerId == customerId);

        if (state != null)
        {
            return state;
        }

        state = new ConversationState
        {
            CustomerId = customerId,
            CurrentStep = "New"
        };

        _db.ConversationStates.Add(state);
        await _db.SaveChangesAsync();
        return state;
    }

    public async Task UpdateAsync(Customer customer, DetectedIntent intent, string message)
    {
        var state = await GetOrCreate(customer.Id);
        var previousStep = state.CurrentStep;

        state.TurnCount += 1;
        state.LastIntent = intent.Label;
        state.CurrentIntent = intent.Label;
        state.LastCustomerMessageAt = DateTime.UtcNow;
        state.UpdatedAt = DateTime.UtcNow;

        var selectedService = intent.ServiceName;
        if (string.IsNullOrWhiteSpace(selectedService) && previousStep == "AwaitingService")
        {
            var lowerMessage = message.ToLowerInvariant();
            var serviceNames = await _db.Services
                .AsNoTracking()
                .Where(x => x.Active)
                .Select(x => x.Name)
                .ToListAsync();
            selectedService = serviceNames
                .OrderByDescending(x => x.Length)
                .FirstOrDefault(x => lowerMessage.Contains(x.ToLowerInvariant(), StringComparison.Ordinal));
        }

        if (!string.IsNullOrWhiteSpace(selectedService))
        {
            state.SelectedService = selectedService;
        }

        var lower = message.ToLowerInvariant();
        if (lower.Contains("prefer") || lower.Contains("i like") || lower.Contains("usually"))
        {
            state.Preferences = message.Length <= 240 ? message : message[..240];
            customer.Notes = state.Preferences;
        }

        var hasTimeLanguage = lower.Contains("am") ||
                              lower.Contains("pm") ||
                              lower.Contains("today") ||
                              lower.Contains("tomorrow") ||
                              System.Text.RegularExpressions.Regex.IsMatch(message, @"\b\d{1,2}:\d{2}\b") ||
                              System.Text.RegularExpressions.Regex.IsMatch(lower, @"\b(?:at|around|about)\s+(?:1[0-2]|[1-9])\b") ||
                              (previousStep == "AwaitingTime" &&
                               System.Text.RegularExpressions.Regex.IsMatch(lower, @"\b(?:1[0-2]|[1-9])\b"));

        state.CurrentStep = intent.Kind switch
        {
            ReignIntentKind.Schedule when message.Contains("YES", StringComparison.OrdinalIgnoreCase) => "AwaitingConfirm",
            ReignIntentKind.Schedule when !string.IsNullOrWhiteSpace(selectedService) && !hasTimeLanguage
                => "AwaitingTime",
            ReignIntentKind.Confirm => "Confirmed",
            ReignIntentKind.Cancel => "Cancelled",
            ReignIntentKind.NameCapture => "Active",
            ReignIntentKind.Greeting or ReignIntentKind.Unknown when string.IsNullOrWhiteSpace(customer.Name)
                => "AwaitingName",
            _ => string.IsNullOrWhiteSpace(state.CurrentStep) || state.CurrentStep == "None" || state.CurrentStep == "New"
                ? "Active"
                : state.CurrentStep
        };

        await _db.SaveChangesAsync();
    }

    public string Describe(ConversationState state)
    {
        return
            $"status={state.CurrentStep}; " +
            $"intent={state.CurrentIntent ?? "none"}; " +
            $"pendingService={state.SelectedService ?? "none"}; " +
            $"turns={state.TurnCount}";
    }
}
