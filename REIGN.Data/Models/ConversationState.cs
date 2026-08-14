namespace REIGN.Data.Models;

public class ConversationState
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid CustomerId { get; set; }

    public Customer Customer { get; set; } = null!;

    public string CurrentStep { get; set; } = "None";

    public string? CurrentIntent { get; set; }

    public string? LastIntent { get; set; }

    public string? SelectedService { get; set; }

    public DateTime? RequestedTime { get; set; }

    public string? Location { get; set; }

    public string? Preferences { get; set; }

    public int TurnCount { get; set; }

    public DateTime? LastCustomerMessageAt { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
