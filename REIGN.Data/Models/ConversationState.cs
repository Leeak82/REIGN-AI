namespace REIGN.Data.Models;

public class ConversationState
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid CustomerId { get; set; }

    public Customer Customer { get; set; } = null!;

    public string CurrentStep { get; set; } = "None";

    public string? SelectedService { get; set; }

    public DateTime? RequestedTime { get; set; }

    public string? Location { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
