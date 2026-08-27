using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace REIGN.API.Messaging;

/// <summary>
/// SkipCalls / Zapier inbound SMS. Accepts the public API shape and the
/// Zapier "New SMS Received" payload (from/to/body or phoneNumber/content).
/// </summary>
public static class SkipCallsWebhookValidator
{
    public static bool IsAuthorized(string expectedSecret, params string?[] provided)
    {
        if (string.IsNullOrWhiteSpace(expectedSecret))
        {
            return false;
        }

        var expected = Encoding.UTF8.GetBytes(expectedSecret.Trim());
        foreach (var raw in provided)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                continue;
            }

            var value = raw.Trim();
            if (value.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                value = value["Bearer ".Length..].Trim();
            }

            var actual = Encoding.UTF8.GetBytes(value);
            if (expected.Length == actual.Length &&
                CryptographicOperations.FixedTimeEquals(expected, actual))
            {
                return true;
            }
        }

        return false;
    }

    public static IncomingSmsMessage? TryParseReceived(string rawBody)
    {
        if (string.IsNullOrWhiteSpace(rawBody))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(rawBody);
            var root = doc.RootElement;
            var eventName = ReadString(root, "event")
                ?? ReadString(root, "type")
                ?? ReadString(root, "trigger");
            if (LooksLikeOutbound(eventName))
            {
                return null;
            }

            var payload = Unwrap(root);
            var direction = ReadString(payload, "direction") ?? ReadString(payload, "kind");
            if (LooksLikeOutbound(direction))
            {
                return null;
            }

            var from = FirstNonEmpty(
                payload,
                "from",
                "fromNumber",
                "phoneNumberFrom",
                "phone_number_from",
                "sender",
                "customerPhone",
                "customer_phone",
                "phoneNumber",
                "phone_number");
            var to = FirstNonEmpty(
                payload,
                "to",
                "toNumber",
                "phoneNumberTo",
                "phone_number_to",
                "recipient");
            var body = FirstNonEmpty(
                payload,
                "body",
                "message",
                "content",
                "text");
            if (string.IsNullOrWhiteSpace(from) || string.IsNullOrWhiteSpace(body))
            {
                return null;
            }

            return new IncomingSmsMessage
            {
                From = from,
                To = to ?? "",
                Body = body,
                ProviderMessageId = FirstNonEmpty(payload, "messageId", "message_id", "id")
                    ?? FirstNonEmpty(root, "id"),
                Provider = "SkipCalls"
            };
        }
        catch
        {
            return null;
        }
    }

    private static bool LooksLikeOutbound(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var text = value.Trim();
        return text.Contains("sent", StringComparison.OrdinalIgnoreCase)
            || text.Contains("outbound", StringComparison.OrdinalIgnoreCase)
            || text.Contains("failed", StringComparison.OrdinalIgnoreCase);
    }

    private static JsonElement Unwrap(JsonElement root)
    {
        foreach (var name in new[] { "payload", "data", "sms", "message" })
        {
            if (root.TryGetProperty(name, out var nested) && nested.ValueKind == JsonValueKind.Object)
            {
                return nested;
            }
        }

        return root;
    }

    private static string? FirstNonEmpty(JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            var value = ReadString(element, name);
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
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
