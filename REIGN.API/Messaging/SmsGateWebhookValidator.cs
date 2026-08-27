using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace REIGN.API.Messaging;

/// <summary>
/// SMSGate webhook HMAC: hex(HMAC-SHA256(signingKey, rawBody + X-Timestamp)).
/// https://docs.sms-gate.app/features/webhooks/
/// </summary>
public static class SmsGateWebhookValidator
{
    public static readonly TimeSpan ReplayWindow = TimeSpan.FromMinutes(5);

    public static bool IsValid(
        string signingKey,
        string rawBody,
        string? timestampHeader,
        string? signatureHeader,
        DateTimeOffset? now = null)
    {
        if (string.IsNullOrWhiteSpace(signingKey) ||
            string.IsNullOrWhiteSpace(signatureHeader) ||
            string.IsNullOrWhiteSpace(timestampHeader))
        {
            return false;
        }

        if (!long.TryParse(timestampHeader.Trim(), out var unixSeconds))
        {
            return false;
        }

        var clock = now ?? DateTimeOffset.UtcNow;
        var stamped = DateTimeOffset.FromUnixTimeSeconds(unixSeconds);
        if ((clock - stamped).Duration() > ReplayWindow)
        {
            return false;
        }

        var message = rawBody + timestampHeader.Trim();
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(signingKey));
        var expected = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(message)));
        var provided = signatureHeader.Trim();
        if (provided.StartsWith("sha256=", StringComparison.OrdinalIgnoreCase))
        {
            provided = provided["sha256=".Length..];
        }

        var expectedBytes = Encoding.UTF8.GetBytes(expected.ToLowerInvariant());
        var providedBytes = Encoding.UTF8.GetBytes(provided.ToLowerInvariant());
        return expectedBytes.Length == providedBytes.Length &&
               CryptographicOperations.FixedTimeEquals(expectedBytes, providedBytes);
    }

    public static string ComputeSignature(string signingKey, string rawBody, string timestamp)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(signingKey));
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(rawBody + timestamp)))
            .ToLowerInvariant();
    }

    public static IncomingSmsMessage? TryParseReceived(string rawBody) =>
        ParseReceived(rawBody, out _).FirstOrDefault();

    public static IReadOnlyList<IncomingSmsMessage> ParseReceived(string rawBody, out string? eventName)
    {
        eventName = null;
        if (string.IsNullOrWhiteSpace(rawBody))
        {
            return [];
        }

        try
        {
            using var doc = JsonDocument.Parse(rawBody);
            var root = doc.RootElement;
            eventName = ReadString(root, "event");
            if (!root.TryGetProperty("payload", out var payload) || payload.ValueKind != JsonValueKind.Object)
            {
                payload = root;
            }

            if (string.Equals(eventName, "sms:batch:received", StringComparison.OrdinalIgnoreCase))
            {
                if (!payload.TryGetProperty("messages", out var messages) ||
                    messages.ValueKind != JsonValueKind.Array)
                {
                    return [];
                }

                var batch = new List<IncomingSmsMessage>();
                foreach (var item in messages.EnumerateArray())
                {
                    var parsed = ParsePayload(item, root);
                    if (parsed != null)
                    {
                        batch.Add(parsed);
                    }
                }

                return batch;
            }

            if (!string.IsNullOrWhiteSpace(eventName) &&
                !eventName.Equals("sms:received", StringComparison.OrdinalIgnoreCase))
            {
                return [];
            }

            var single = ParsePayload(payload, root);
            return single == null ? [] : [single];
        }
        catch
        {
            return [];
        }
    }

    private static IncomingSmsMessage? ParsePayload(JsonElement payload, JsonElement root)
    {
        if (payload.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var sender = ReadString(payload, "sender");
        var reported = ReadString(payload, "phoneNumber");
        var from = sender ?? reported;
        var to = ReadString(payload, "recipient");
        var body = ReadString(payload, "message") ?? ReadString(payload, "text");
        if (string.IsNullOrWhiteSpace(from) || string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        return new IncomingSmsMessage
        {
            From = from,
            To = to ?? "",
            Body = body,
            ProviderMessageId = ReadString(payload, "messageId") ?? ReadString(root, "id"),
            Provider = "SmsGate",
            SimNumber = ReadInt(payload, "simNumber"),
            ReportedPhoneNumber = reported
        };
    }

    private static int? ReadInt(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value))
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number))
        {
            return number;
        }

        if (value.ValueKind == JsonValueKind.String &&
            int.TryParse(value.GetString(), out var parsed))
        {
            return parsed;
        }

        return null;
    }

    private static string? ReadString(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.ToString(),
            _ => null
        };
    }
}
