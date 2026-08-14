namespace REIGN.Data.Models;

public class Customer
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string PhoneNumber { get; set; } = "";

    public string? Name { get; set; }

    public string? Notes { get; set; }

    /// <summary>
    /// When true, inbound customer SMS is stored but the assistant does not auto-reply.
    /// The owner is speaking for REIGN through the inbox / outbound SMS path.
    /// </summary>
    public bool HumanOverrideActive { get; set; }

    public DateTime? HumanOverrideAt { get; set; }

    public string? CurrentIntent { get; set; }

    public string? LastIntent { get; set; }

    public string? PendingServiceName { get; set; }

    public string? ConversationStatus { get; set; }

    public int TurnCount { get; set; }

    public DateTime? LastCustomerMessageAt { get; set; }

    public string? IntentHistory { get; set; }

    public string? MemorySummary { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<Appointment> Appointments { get; set; } = new();

    public List<ConversationMessage> Messages { get; set; } = new();
}
