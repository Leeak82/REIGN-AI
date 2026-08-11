namespace REIGN.Core.Models;

public class Conversation
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;

    public string Channel { get; set; } = "SMS";
    public string Status { get; set; } = "Active";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public List<ConversationMessage> Messages { get; set; } = [];
}
