namespace REIGN.Data.Models;

public class BusinessAIProfile
{
    public Guid Id { get; set; }

    public Guid BusinessId { get; set; }

    public string AIName { get; set; } = "REIGN";

    public string Personality { get; set; } =
        "Professional, friendly, and efficient.";

    public string Greeting { get; set; } =
        "Hello! I can help schedule your service appointment.";

    public string BusinessDescription { get; set; } =
        "AI-powered appointment assistant.";

    public bool Active { get; set; } = true;

    public Business? Business { get; set; }
}
