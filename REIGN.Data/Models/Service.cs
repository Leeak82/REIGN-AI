namespace REIGN.Data.Models;

public class Service
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid? BusinessId { get; set; }

    public string Name { get; set; } = "";

    public decimal Price { get; set; }

    public int DurationMinutes { get; set; }

    public bool Active { get; set; } = true;

    public Business? Business { get; set; }
}
