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

        var syncNumber = SearchQueries(phone).FirstOrDefault()
            ?? new string(phone.Where(char.IsDigit).ToArray());
        using var sync = Authenticated(HttpMethod.Post, "/users/me/contacts/sync");
        sync.Content = JsonContent(new
        {
            contacts = new[]
            {
                new
                {
                    phoneNumber = syncNumber,
                    firstName = "Customer",
                    companyName = "Miss Reign",
                    source = "WEB"
                }
            }
        });
        using var syncResponse = await _http.SendAsync(sync, cancellationToken);
        var syncBody = await syncResponse.Content.ReadAsStringAsync(cancellationToken);
        if (!syncResponse.IsSuccessStatusCode)
        {
            _logger.LogWarning("SkipCalls contact sync failed: {Status} {Body}", (int)syncResponse.StatusCode, Truncate(syncBody));
            return null;
        }

        var syncedId = TryReadContactId(syncBody);
        if (!string.IsNullOrWhiteSpace(syncedId))
        {
            return syncedId;
        }

        return await SearchContactIdAsync(phone, cancellationToken);
    }

    private async Task<string?> SearchContactIdAsync(string phone, CancellationToken cancellationToken)
    {
        foreach (var query in SearchQueries(phone))
        {
            var id = await SearchContactIdOnceAsync(phone, query, cancellationToken);
            if (!string.IsNullOrWhiteSpace(id))
            {
                return id;
            }
        }

        return null;
    }

    private async Task<string?> SearchContactIdOnceAsync(string phone, string query, CancellationToken cancellationToken)
    {
        using var search = Authenticated(
            HttpMethod.Post,
            "/users/me/contacts/search?query=" + Uri.EscapeDataString(query) + "&limit=10");
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

    // SkipCalls search ignores E.164 with a leading plus (`+1253...` returns no contacts).
    internal static IReadOnlyList<string> SearchQueries(string phone)
    {
        var queries = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var digits = new string(phone.Where(char.IsDigit).ToArray());

        void Add(string? value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.StartsWith('+') || !seen.Add(value))
            {
                return;
            }

            queries.Add(value);
        }

        Add(digits);
        if (digits.Length == 11 && digits.StartsWith('1'))
        {
            Add(digits[1..]);
        }
        else if (digits.Length == 10)
        {
            Add("1" + digits);
        }

        if (queries.Count == 0)
        {
            Add(phone);
        }

        return queries;
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
            return ReadId(doc.RootElement, "id", "messageId", "smsId");
        }
        catch
        {
        }

        return null;
    }

    private static string? TryReadContactId(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var id = ReadId(root, "id", "contactId");
            if (!string.IsNullOrWhiteSpace(id))
            {
                return id;
            }

            foreach (var name in new[] { "contacts", "created", "updated", "data" })
            {
                if (!root.TryGetProperty(name, out var node))
                {
                    continue;
                }

                if (node.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in node.EnumerateArray())
                    {
                        id = ReadId(item, "id", "contactId");
                        if (!string.IsNullOrWhiteSpace(id))
                        {
                            return id;
                        }
                    }
                }
                else if (node.ValueKind == JsonValueKind.Object)
                {
                    id = ReadId(node, "id", "contactId");
                    if (!string.IsNullOrWhiteSpace(id))
                    {
                        return id;
                    }
                }
            }
        }
        catch
        {
        }

        return null;
    }

    private static string? ReadId(JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            if (element.TryGetProperty(name, out var id) && id.ValueKind is JsonValueKind.String or JsonValueKind.Number)
            {
                var value = id.ValueKind == JsonValueKind.String ? id.GetString() : id.ToString();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }
        }

        return null;
    }
}
