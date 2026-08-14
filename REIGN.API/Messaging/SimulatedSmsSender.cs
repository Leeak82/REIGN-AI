using System.Collections.Concurrent;

namespace REIGN.API.Messaging;

public class SimulatedOutboundSms
{
    public DateTime SentAt { get; set; } = DateTime.UtcNow;

    public string From { get; set; } = "";

    public string To { get; set; } = "";

    public string Body { get; set; } = "";

    public string Id { get; set; } = Guid.NewGuid().ToString("N");
}

public class SimulatedSmsSender : ISmsSender
{
    private readonly ConcurrentQueue<SimulatedOutboundSms> _sent = new();

    public string ProviderName => "Simulated";

    public bool IsConfigured => true;

    public bool IsSimulated => true;

    public IReadOnlyList<SimulatedOutboundSms> Sent => _sent.ToArray();

    public Task<SmsSendResult> SendAsync(SmsSendRequest request, CancellationToken cancellationToken = default)
    {
        var message = new SimulatedOutboundSms
        {
            From = request.From ?? "",
            To = request.To,
            Body = request.Body
        };

        _sent.Enqueue(message);

        while (_sent.Count > 200 && _sent.TryDequeue(out _))
        {
        }

        return Task.FromResult(SmsSendResult.Ok(ProviderName, message.Id, simulated: true));
    }
}
