namespace REIGN.Data.Models;

public class Business
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string OwnerName { get; set; } = string.Empty;

    public string Phone { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string Address { get; set; } = string.Empty;

    public string Industry { get; set; } = string.Empty;

    public bool Active { get; set; } = true;

    public string Greeting { get; set; } = string.Empty;

    public string Tone { get; set; } = string.Empty;

    public string Personality { get; set; } = string.Empty;

    public string Instructions { get; set; } = string.Empty;

    public string Hours { get; set; } = string.Empty;

    public string TimeZone { get; set; } = "America/New_York";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Customer> Customers { get; set; } = new List<Customer>();

    public ICollection<Service> Services { get; set; } = new List<Service>();

    public ICollection<BusinessAIProfile> AIProfiles { get; set; } = new List<BusinessAIProfile>();
}
