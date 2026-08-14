namespace REIGN.Data.Models;

public class CustomerIntentMemory
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid CustomerId { get; set; }

    public string Intent { get; set; } = "";

    public string? SelectedService { get; set; }

    public string Stage { get; set; } = "New";

    public string? Summary { get; set; }

    public string? HistoryJson { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Customer? Customer { get; set; }
}
