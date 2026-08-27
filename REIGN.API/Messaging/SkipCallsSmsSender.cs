using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using REIGN.API.Options;

namespace REIGN.API.Messaging;

public class SkipCallsSmsSender : ISmsSender
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _http;
    private readonly SmsOptions _options;
    private readonly ILogger<SkipCallsSmsSender> _logger;

    public SkipCallsSmsSender(HttpClient http, IOptions<SmsOptions> options, ILogger<SkipCallsSmsSender> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;
    }

    public string ProviderName => "SkipCalls";

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_options.SkipCalls.AccessToken);

    public bool IsSimulated => false;

    public async Task<SmsSendResult> SendAsync(SmsSendRequest request, CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            return SmsSendResult.Fail(ProviderName, "SkipCalls AccessToken is not configured. Set Sms__SkipCalls__AccessToken.");
        }

        var from = BusinessNumberGuard.ResolveFromNumber(
            _options,
            FirstNonEmpty(_options.SkipCalls.FromNumber, _options.BusinessPhoneNumber),
            request.From);
        if (from.Error != null)
        {
            return SmsSendResult.Fail(ProviderName, from.Error);
        }

        var dest = PhoneNumbers.Normalize(request.To);
        var ownNumbers = PhoneNumbers.GatewayOwnNumbers(
            _options.BusinessPhoneNumber,
            _options.SmsGate.FromNumber,
            _options.SmsGate.IgnoreFromNumbers,
            _options.SkipCalls.FromNumber);
        if (PhoneNumbers.IsOwnDeviceNumber(dest, ownNumbers))
        {
            _logger.LogWarning("Refusing SkipCalls send to the SkipCalls number {Phone}", dest);
            return SmsSendResult.Fail(ProviderName, "Refusing to text the SkipCalls business number.");
        }

        try
        {
            var contactId = await FindOrCreateContactIdAsync(dest, cancellationToken);
            if (string.IsNullOrWhiteSpace(contactId))
            {
                return SmsSendResult.Fail(ProviderName, "SkipCalls could not find or create a contact for that number.");
            }

            using var message = Authenticated(HttpMethod.Post, $"/users/me/contacts/{Uri.EscapeDataString(contactId)}/send-sms");
            var payload = new Dictionary<string, string> { ["message"] = request.Body };
            if (!string.IsNullOrWhiteSpace(_options.SkipCalls.AgentId))
            {
                payload["agentId"] = _options.SkipCalls.AgentId.Trim();
            }

            message.Content = JsonContent(payload);
            using var response = await _http.SendAsync(message, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "SkipCalls send failed: {Status} From={From} To={To} {Body}",
                    (int)response.StatusCode,
                    from.Number,
                    dest,
                    Truncate(body));
                return SmsSendResult.Fail(ProviderName, $"SkipCalls HTTP {(int)response.StatusCode}");
            }

            var id = TryReadId(body);
            _logger.LogInformation("SkipCalls send accepted From={From} To={To} Id={Id}", from.Number, dest, id);
            return SmsSendResult.Ok(ProviderName, id);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SkipCalls send threw");
            return SmsSendResult.Fail(ProviderName, ex.Message);
        }
    }

    private async Task<string?> FindOrCreateContactIdAsync(string phone, CancellationToken cancellationToken)
    {
        var existing = await SearchContactIdAsync(phone, cancellationToken);
        if (!string.IsNullOrWhiteSpace(existing))
        {
            return existing;
        }

        using var sync = Authenticated(HttpMethod.Post, "/users/me/contacts/sync");
        sync.Content = JsonContent(new
        {
            contacts = new[]
            {
                new
                {
                    phoneNumber = phone,
                    firstName = "Customer",
                    companyName = "Miss Reign",
                    source = "WEB"
                }
            }
        });
        using var syncResponse = await _http.SendAsync(sync, cancellationToken);
        if (!syncResponse.IsSuccessStatusCode)
        {
            var syncBody = await syncResponse.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning("SkipCalls contact sync failed: {Status} {Body}", (int)syncResponse.StatusCode, Truncate(syncBody));
            return null;
        }

        return await SearchContactIdAsync(phone, cancellationToken);
    }

    private async Task<string?> SearchContactIdAsync(string phone, CancellationToken cancellationToken)
    {
        using var search = Authenticated(
            HttpMethod.Post,
            "/users/me/contacts/search?query=" + Uri.EscapeDataString(phone) + "&limit=10");
        using var response = await _http.SendAsync(search, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("SkipCalls contact search failed: {Status} {Body}", (int)response.StatusCode, Truncate(body));
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(body);
            if (!doc.RootElement.TryGetProperty("contacts", out var contacts) ||
                contacts.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            foreach (var contact in contacts.EnumerateArray())
            {
                var number = contact.TryGetProperty("phoneNumber", out var phoneEl)
                    ? phoneEl.GetString()
                    : null;
                if (!PhoneNumbers.AreSame(number, phone))
                {
                    continue;
                }

                if (contact.TryGetProperty("id", out var id))
                {
                    return id.ValueKind == JsonValueKind.String ? id.GetString() : id.ToString();
                }
            }
        }
        catch
        {
        }

        return null;
    }

    private HttpRequestMessage Authenticated(HttpMethod method, string path)
    {
        var baseUrl = string.IsNullOrWhiteSpace(_options.SkipCalls.BaseUrl)
            ? SkipCallsOptions.DefaultBaseUrl
            : _options.SkipCalls.BaseUrl.TrimEnd('/');
        var request = new HttpRequestMessage(method, baseUrl + path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.SkipCalls.AccessToken.Trim());
        return request;
    }

    private static StringContent JsonContent(object payload) =>
        new(JsonSerializer.Serialize(payload, Json), Encoding.UTF8, "application/json");

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(static v => !string.IsNullOrWhiteSpace(v)) ?? "";

    private static string Truncate(string value) =>
        value.Length <= 300 ? value : value[..300];

    private static string? TryReadId(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            foreach (var name in new[] { "id", "messageId", "smsId" })
            {
                if (doc.RootElement.TryGetProperty(name, out var id))
                {
                    return id.ValueKind == JsonValueKind.String ? id.GetString() : id.ToString();
                }
            }
        }
        catch
        {
        }

        return null;
    }
}
