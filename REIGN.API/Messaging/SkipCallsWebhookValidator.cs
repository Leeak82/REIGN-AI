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

    public static string DescribeKeys(string rawBody)
    {
        try
        {
            using var doc = JsonDocument.Parse(rawBody);
            var names = new SortedSet<string>(StringComparer.Ordinal);
            CollectKeys(doc.RootElement, "", names, depth: 0);
            return names.Count == 0 ? "(none)" : string.Join(",", names);
        }
        catch
        {
            return "(unparsed)";
        }
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
                ?? ReadString(root, "trigger")
                ?? ReadString(root, "eventName");
            if (LooksLikeOutbound(eventName))
            {
                return null;
            }

            var payload = Unwrap(root);
            var sms = ObjectOf(payload, "smsMessage") ?? ObjectOf(root, "smsMessage");
            var phones = ObjectOf(payload, "phoneNumber") ?? ObjectOf(root, "phoneNumber");
            var conversation = ObjectOf(payload, "smsConversation") ?? ObjectOf(root, "smsConversation");
            var direction = ReadText(sms ?? payload, "direction", "kind")
                ?? ReadPhoneOrString(payload, "direction")
                ?? ReadPhoneOrString(payload, "kind");
            if (LooksLikeOutbound(direction))
            {
                return null;
            }

            var contact = ObjectOf(payload, "contact")
                ?? ObjectOf(payload, "customer")
                ?? ObjectOf(root, "contact")
                ?? ObjectOf(payload, "conversation")
                ?? conversation;
            var contactPhone = contact is JsonElement contactEl
                ? ReadPhone(contactEl, "phoneNumber", "phone", "number", "customerPhone", "from")
                : null;

            var from = ReadPhone(phones ?? payload,
                "fromNumber",
                "from",
                "phoneNumberFrom",
                "phone_number_from",
                "sender",
                "customerPhone",
                "customer_phone")
                ?? ReadPhone(payload,
                    "from",
                    "fromNumber",
                    "phoneNumberFrom",
                    "phone_number_from",
                    "sender",
                    "customerPhone",
                    "customer_phone")
                ?? contactPhone;
            var to = ReadPhone(phones ?? payload,
                "toNumber",
                "to",
                "phoneNumberTo",
                "phone_number_to",
                "recipient",
                "agentPhoneNumber",
                "businessPhoneNumber")
                ?? ReadPhone(payload,
                    "to",
                    "toNumber",
                    "phoneNumberTo",
                    "phone_number_to",
                    "recipient",
                    "agentPhoneNumber",
                    "businessPhoneNumber");
            var reported = contactPhone
                ?? ReadPhone(phones ?? payload, "fromNumber", "phoneNumber", "phone_number")
                ?? ReadPhone(payload, "phoneNumber", "phone_number");
            if (string.IsNullOrWhiteSpace(from))
            {
                from = reported;
            }

            var body = ReadText(sms ?? payload, "content", "body", "message", "text", "smsBody")
                ?? ReadText(conversation ?? payload, "lastMessage", "content")
                ?? ReadText(payload, "body", "message", "content", "text", "smsBody");
            if (string.IsNullOrWhiteSpace(from) || string.IsNullOrWhiteSpace(body))
            {
                return null;
            }

            return new IncomingSmsMessage
            {
                From = from,
                To = to ?? "",
                Body = body,
                ProviderMessageId = ReadText(sms ?? payload, "id", "messageId", "message_id", "smsId")
                    ?? ReadText(payload, "messageId", "message_id", "smsId", "id")
                    ?? ReadText(root, "id"),
                Provider = "SkipCalls",
                ReportedPhoneNumber = reported
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
        if (text.Contains("received", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("inbound", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return text.Contains("sent", StringComparison.OrdinalIgnoreCase)
            || text.Contains("outbound", StringComparison.OrdinalIgnoreCase)
            || text.Contains("outgoing", StringComparison.OrdinalIgnoreCase)
            || text.Contains("failed", StringComparison.OrdinalIgnoreCase);
    }

    private static JsonElement Unwrap(JsonElement root)
    {
        foreach (var name in new[] { "payload", "data", "sms", "record" })
        {
            if (root.TryGetProperty(name, out var nested) && nested.ValueKind == JsonValueKind.Object)
            {
                return nested;
            }
        }

        if (root.TryGetProperty("message", out var message) && message.ValueKind == JsonValueKind.Object)
        {
            return message;
        }

        return root;
    }

    private static JsonElement? ObjectOf(JsonElement element, string name)
    {
        if (element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Object)
        {
            return value;
        }

        return null;
    }

    private static string? ReadPhone(JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            if (!element.TryGetProperty(name, out var value))
            {
                continue;
            }

            var phone = PhoneFrom(value);
            if (!string.IsNullOrWhiteSpace(phone))
            {
                return phone;
            }
        }

        return null;
    }

    private static string? PhoneFrom(JsonElement value) =>
        value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.ToString(),
            JsonValueKind.Object => ReadPhone(value, "fromNumber", "toNumber", "phoneNumber", "phone", "number", "e164", "from", "to"),
            _ => null
        };

    private static string? ReadPhoneOrString(JsonElement element, string name) =>
        ReadPhone(element, name) ?? ReadString(element, name);

    private static string? ReadText(JsonElement element, params string[] names)
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

    private static void CollectKeys(JsonElement element, string prefix, ISet<string> names, int depth)
    {
        if (depth > 4)
        {
            return;
        }

        if (element.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        foreach (var property in element.EnumerateObject())
        {
            var path = string.IsNullOrEmpty(prefix) ? property.Name : prefix + "." + property.Name;
            names.Add(path);
            if (property.Value.ValueKind == JsonValueKind.Object)
            {
                CollectKeys(property.Value, path, names, depth + 1);
            }
        }
    }
}
