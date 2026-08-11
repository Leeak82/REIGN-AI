namespace REIGN.Data.Models;

public class ServiceRecommendation
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Trigger { get; set; } = "";

    public string Recommendation { get; set; } = "";

    public Guid ServiceId { get; set; }

    public Service? Service { get; set; }

    public bool Active { get; set; } = true;
}
