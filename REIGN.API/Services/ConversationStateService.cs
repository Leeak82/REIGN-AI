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

    public async Task UpdateAsync(Customer customer, DetectedIntent intent, string message)
    {
        customer.TurnCount += 1;
        customer.LastIntent = intent.Label;
        customer.CurrentIntent = intent.Label;
        customer.LastCustomerMessageAt = DateTime.UtcNow;

        if (!string.IsNullOrWhiteSpace(intent.ServiceName))
        {
            customer.PendingServiceName = intent.ServiceName;
        }

        customer.ConversationStatus = intent.Kind switch
        {
            ReignIntentKind.Schedule when message.Contains("YES", StringComparison.OrdinalIgnoreCase) => "AwaitingConfirm",
            ReignIntentKind.Schedule when !string.IsNullOrWhiteSpace(intent.ServiceName) &&
                                          !(message.ToLowerInvariant().Contains("am") ||
                                            message.ToLowerInvariant().Contains("pm") ||
                                            message.ToLowerInvariant().Contains("today") ||
                                            message.ToLowerInvariant().Contains("tomorrow"))
                => "AwaitingTime",
            ReignIntentKind.Confirm => "Confirmed",
            ReignIntentKind.Cancel => "Cancelled",
            ReignIntentKind.NameCapture => "Active",
            _ => string.IsNullOrWhiteSpace(customer.ConversationStatus) ? "Active" : customer.ConversationStatus
        };

        await _db.SaveChangesAsync();
    }

    public string Describe(Customer customer)
    {
        return
            $"status={customer.ConversationStatus ?? "Active"}; " +
            $"intent={customer.CurrentIntent ?? "none"}; " +
            $"pendingService={customer.PendingServiceName ?? "none"}; " +
            $"turns={customer.TurnCount}";
    }
}
