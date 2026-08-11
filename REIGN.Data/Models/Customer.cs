namespace REIGN.Data.Models;

public class Customer
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string PhoneNumber { get; set; } = "";

    public string? Name { get; set; }

    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<Appointment> Appointments { get; set; } = new();

    public List<ConversationMessage> Messages { get; set; } = new();
}
