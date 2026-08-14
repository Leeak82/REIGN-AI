namespace REIGN.API.Options;

public class BusinessProfileOptions
{
    public const string SectionName = "Business";

    public string Name { get; set; } = "REIGN";

    public string AssistantName { get; set; } = "Miss Reign";

    public string Offering { get; set; } = "Private appointment scheduling for Quick Visit, Half Hour, and Hour sessions.";

    public string Hours { get; set; } = "By appointment, typically 9am–5pm. Same-day visits need at least one hour notice.";

    public string Tone { get; set; } = "Warm, concise, professional. SMS replies stay under 320 characters.";

    public string TimeZone { get; set; } = "America/New_York";
}

public class AiOptions
{
    public const string SectionName = "Ai";

    /// <summary>
    /// Groq or Fallback. Missing Groq credentials always fall back.
    /// </summary>
    public string Provider { get; set; } = "Groq";

    public string ApiKey { get; set; } = "";

    public string BaseUrl { get; set; } = "https://api.groq.com/openai/v1/";

    public string Model { get; set; } = "llama-3.3-70b-versatile";

    public int TimeoutSeconds { get; set; } = 20;

    public int MaxTokens { get; set; } = 220;
}
