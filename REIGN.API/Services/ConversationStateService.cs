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

        state.TurnCount += 1;
        state.LastIntent = intent.Label;
        state.CurrentIntent = intent.Label;
        state.LastCustomerMessageAt = DateTime.UtcNow;
        state.UpdatedAt = DateTime.UtcNow;

        if (!string.IsNullOrWhiteSpace(intent.ServiceName))
        {
            state.SelectedService = intent.ServiceName;
        }

        var lower = message.ToLowerInvariant();
        if (lower.Contains("prefer") || lower.Contains("i like") || lower.Contains("usually"))
        {
            state.Preferences = message.Length <= 240 ? message : message[..240];
            customer.Notes = state.Preferences;
        }

        state.CurrentStep = intent.Kind switch
        {
            ReignIntentKind.Schedule when message.Contains("YES", StringComparison.OrdinalIgnoreCase) => "AwaitingConfirm",
            ReignIntentKind.Schedule when !string.IsNullOrWhiteSpace(intent.ServiceName) &&
                                          !(message.ToLowerInvariant().Contains("am") ||
                                            message.ToLowerInvariant().Contains("pm") ||
                                            message.ToLowerInvariant().Contains("today") ||
                                            message.ToLowerInvariant().Contains("tomorrow") ||
                                            System.Text.RegularExpressions.Regex.IsMatch(message, @"\b\d{1,2}:\d{2}\b"))
                => "AwaitingTime",
            ReignIntentKind.Confirm => "Confirmed",
            ReignIntentKind.Cancel => "Cancelled",
            ReignIntentKind.NameCapture => "Active",
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
