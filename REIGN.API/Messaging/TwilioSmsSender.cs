using System.Net.Http.Headers;
using System.Text;
using Microsoft.Extensions.Options;
using REIGN.API.Options;

namespace REIGN.API.Messaging;

public class TwilioSmsSender : ISmsSender
{
    private readonly HttpClient _http;
    private readonly SmsOptions _options;
    private readonly ILogger<TwilioSmsSender> _logger;

    public TwilioSmsSender(HttpClient http, IOptions<SmsOptions> options, ILogger<TwilioSmsSender> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;
    }

    public string ProviderName => "Twilio";

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(_options.Twilio.AccountSid) &&
        !string.IsNullOrWhiteSpace(_options.Twilio.AuthToken);

    public bool IsSimulated => false;

    public async Task<SmsSendResult> SendAsync(SmsSendRequest request, CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            return SmsSendResult.Fail(ProviderName, "Twilio AccountSid/AuthToken are not configured.");
        }

        var from = BusinessNumberGuard.ResolveFromNumber(_options, _options.Twilio.FromNumber, request.From);
        if (from.Error != null)
        {
            return SmsSendResult.Fail(ProviderName, from.Error);
        }

        var url = $"https://api.twilio.com/2010-04-01/Accounts/{_options.Twilio.AccountSid}/Messages.json";
        using var message = new HttpRequestMessage(HttpMethod.Post, url);
        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_options.Twilio.AccountSid}:{_options.Twilio.AuthToken}"));
        message.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
        message.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["To"] = PhoneNumbers.Normalize(request.To),
            ["From"] = from.Number,
            ["Body"] = request.Body
        });

        try
        {
            using var response = await _http.SendAsync(message, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Twilio send failed: {Status} From={From} To={To} {Body}",
                    (int)response.StatusCode,
                    from.Number,
                    PhoneNumbers.Normalize(request.To),
                    Truncate(body));
                return SmsSendResult.Fail(ProviderName, $"Twilio HTTP {(int)response.StatusCode}");
            }

            var id = TryReadJsonField(body, "sid");
            return SmsSendResult.Ok(ProviderName, id);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Twilio send threw");
            return SmsSendResult.Fail(ProviderName, ex.Message);
        }
    }

    private static string Truncate(string value) =>
        value.Length <= 300 ? value : value[..300];

    private static string? TryReadJsonField(string json, string field)
    {
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            return doc.RootElement.TryGetProperty(field, out var el) ? el.GetString() : null;
        }
        catch
        {
            return null;
        }
    }
}
