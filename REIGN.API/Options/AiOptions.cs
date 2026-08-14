namespace REIGN.API.Options;

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
