namespace REIGN.Data.Models;

public partial class Service
{
    public Guid Id { get; set; }

    public Guid? BusinessId { get; set; }

    public string Name { get; set; } = "";

    public string? Description { get; set; }

    public decimal Price { get; set; }

    public int DurationMinutes { get; set; }

    public bool Active { get; set; }

    public Business? Business { get; set; }
}
