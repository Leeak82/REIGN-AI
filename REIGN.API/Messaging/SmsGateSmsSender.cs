using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using REIGN.API.Options;

namespace REIGN.API.Messaging;

public class SmsGateSmsSender : ISmsSender
{
    public const string DefaultBaseUrl = "https://api.sms-gate.app/3rdparty/v1";

    private readonly HttpClient _http;
    private readonly SmsOptions _options;
    private readonly ILogger<SmsGateSmsSender> _logger;

    public SmsGateSmsSender(HttpClient http, IOptions<SmsOptions> options, ILogger<SmsGateSmsSender> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;
    }

    public string ProviderName => "SmsGate";

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(_options.SmsGate.Username) &&
        !string.IsNullOrWhiteSpace(_options.SmsGate.Password);

    public bool IsSimulated => false;

    public async Task<SmsSendResult> SendAsync(SmsSendRequest request, CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            return SmsSendResult.Fail(ProviderName, "SmsGate Username/Password are not configured.");
        }

        var from = BusinessNumberGuard.ResolveFromNumber(_options, _options.SmsGate.FromNumber, request.From);
        if (from.Error != null)
        {
            return SmsSendResult.Fail(ProviderName, from.Error);
        }

        var baseUrl = string.IsNullOrWhiteSpace(_options.SmsGate.BaseUrl)
            ? DefaultBaseUrl
            : _options.SmsGate.BaseUrl.TrimEnd('/');
        var url = baseUrl + "/messages";

        using var message = new HttpRequestMessage(HttpMethod.Post, url);
        var credentials = Convert.ToBase64String(
            Encoding.UTF8.GetBytes($"{_options.SmsGate.Username}:{_options.SmsGate.Password}"));
        message.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
        var payload = new Dictionary<string, object?>
        {
            ["textMessage"] = new Dictionary<string, string> { ["text"] = request.Body },
            ["phoneNumbers"] = new[] { PhoneNumbers.Normalize(request.To) }
        };
        if (!string.IsNullOrWhiteSpace(_options.SmsGate.DeviceId))
        {
            payload["deviceId"] = _options.SmsGate.DeviceId.Trim();
        }

        if (_options.SmsGate.SimNumber is >= 1 and <= 3)
        {
            payload["simNumber"] = _options.SmsGate.SimNumber;
        }

        message.Content = new StringContent(
            JsonSerializer.Serialize(payload),
            Encoding.UTF8,
            "application/json");

        try
        {
            using var response = await _http.SendAsync(message, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "SmsGate send failed: {Status} From={From} To={To} {Body}",
                    (int)response.StatusCode,
                    from.Number,
                    PhoneNumbers.Normalize(request.To),
                    Truncate(body));
                return SmsSendResult.Fail(ProviderName, $"SmsGate HTTP {(int)response.StatusCode}");
            }

            return SmsSendResult.Ok(ProviderName, TryReadId(body));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SmsGate send threw");
            return SmsSendResult.Fail(ProviderName, ex.Message);
        }
    }

    private static string Truncate(string value) =>
        value.Length <= 300 ? value : value[..300];

    private static string? TryReadId(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("id", out var id))
            {
                return id.GetString();
            }

            if (doc.RootElement.TryGetProperty("messageId", out var messageId))
            {
                return messageId.GetString();
            }
        }
        catch
        {
        }

        return null;
    }
}
