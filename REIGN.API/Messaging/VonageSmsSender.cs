using System.Text.Json;
using Microsoft.Extensions.Options;
using REIGN.API.Options;

namespace REIGN.API.Messaging;

public class VonageSmsSender : ISmsSender
{
    private readonly HttpClient _http;
    private readonly SmsOptions _options;
    private readonly ILogger<VonageSmsSender> _logger;

    public VonageSmsSender(HttpClient http, IOptions<SmsOptions> options, ILogger<VonageSmsSender> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;
    }

    public string ProviderName => "Vonage";

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(_options.Vonage.ApiKey) &&
        !string.IsNullOrWhiteSpace(_options.Vonage.ApiSecret);

    public bool IsSimulated => false;

    public async Task<SmsSendResult> SendAsync(SmsSendRequest request, CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            return SmsSendResult.Fail(ProviderName, "Vonage ApiKey/ApiSecret are not configured.");
        }

        var from = BusinessNumberGuard.ResolveFromNumber(_options, _options.Vonage.FromNumber, request.From);
        if (from.Error != null)
        {
            return SmsSendResult.Fail(ProviderName, from.Error);
        }

        using var message = new HttpRequestMessage(HttpMethod.Post, "https://rest.nexmo.com/sms/json");
        message.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["api_key"] = _options.Vonage.ApiKey,
            ["api_secret"] = _options.Vonage.ApiSecret,
            ["to"] = PhoneNumbers.Normalize(request.To).TrimStart('+'),
            ["from"] = from.Number.TrimStart('+'),
            ["text"] = request.Body
        });

        try
        {
            using var response = await _http.SendAsync(message, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Vonage send failed: {Status} {Body}", (int)response.StatusCode, Truncate(body));
                return SmsSendResult.Fail(ProviderName, $"Vonage HTTP {(int)response.StatusCode}");
            }

            var id = TryReadMessageId(body);
            return SmsSendResult.Ok(ProviderName, id);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Vonage send threw");
            return SmsSendResult.Fail(ProviderName, ex.Message);
        }
    }

    private static string Truncate(string value) =>
        value.Length <= 300 ? value : value[..300];

    private static string? TryReadMessageId(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("messages", out var messages) &&
                messages.ValueKind == JsonValueKind.Array &&
                messages.GetArrayLength() > 0 &&
                messages[0].TryGetProperty("message-id", out var id))
            {
                return id.GetString();
            }
        }
        catch
        {
        }

        return null;
    }
}
