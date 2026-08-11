namespace REIGN.Core.Models;

public class ConversationMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ConversationId { get; set; }
    public Conversation Conversation { get; set; } = null!;

    public string Direction { get; set; } = "Inbound";
    public string SenderType { get; set; } = "Customer";
    public string Body { get; set; } = "";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
