namespace REIGN.Data.Models;

public class ConversationMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid CustomerId { get; set; }

    public Customer Customer { get; set; } = null!;

    public string Direction { get; set; } = "";

    public string Body { get; set; } = "";

    /// <summary>
    /// Customer, Assistant, Owner, or System.
    /// </summary>
    public string Source { get; set; } = "";

    public bool IsOwnerOverride { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
