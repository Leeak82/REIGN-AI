namespace REIGN.Core.Models;

public class Business
{
    public Guid Id { get; set; }

    public string Name { get; set; } = "";

    public string Phone { get; set; } = "";

    public string? Address { get; set; }

    public bool Active { get; set; } = true;

    public ICollection<Service> Services { get; set; } = new List<Service>();
}
