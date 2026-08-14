namespace REIGN.Data.Models;

public class BusinessAIProfile
{
    public Guid Id { get; set; }

    public Guid BusinessId { get; set; }

    public string AIName { get; set; } = "Miss Reign";

    public string Personality { get; set; } =
        "Warm, concise, professional. SMS replies stay under 320 characters.";

    public string Greeting { get; set; } =
        "Welcome to REIGN. How can we help you schedule a visit today?";

    public string BusinessDescription { get; set; } =
        "Private appointment scheduling for Quick Visit, Half Hour, and Hour sessions.";

    public bool Active { get; set; } = true;

    public Business? Business { get; set; }
}
