namespace REIGN.API.Messaging;

public class IncomingSmsMessage
{
    public string From { get; set; } = "";

    public string To { get; set; } = "";

    public string Body { get; set; } = "";

    public string? ProviderMessageId { get; set; }

    public string Provider { get; set; } = "";
}

public class SmsSendRequest
{
    public string To { get; set; } = "";

    public string Body { get; set; } = "";

    public string? From { get; set; }
}

public class SmsSendResult
{
    public bool Succeeded { get; set; }

    public bool Simulated { get; set; }

    public string Provider { get; set; } = "";

    public string? ProviderMessageId { get; set; }

    public string? Error { get; set; }

    public static SmsSendResult Ok(string provider, string? id = null, bool simulated = false) =>
        new()
        {
            Succeeded = true,
            Simulated = simulated,
            Provider = provider,
            ProviderMessageId = id
        };

    public static SmsSendResult Fail(string provider, string error, bool simulated = false) =>
        new()
        {
            Succeeded = false,
            Simulated = simulated,
            Provider = provider,
            Error = error
        };
}

public interface ISmsSender
{
    string ProviderName { get; }

    bool IsConfigured { get; }

    bool IsSimulated { get; }

    Task<SmsSendResult> SendAsync(SmsSendRequest request, CancellationToken cancellationToken = default);
}
