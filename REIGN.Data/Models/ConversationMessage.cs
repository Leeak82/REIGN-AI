namespace REIGN.Data.Models;

public class ConversationMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid CustomerId { get; set; }

    public Customer Customer { get; set; } = null!;

    public string Direction { get; set; } = "";

    public string Body { get; set; } = "";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
