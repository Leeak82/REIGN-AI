using System.Security.Cryptography;
using System.Text;
using REIGN.API.Messaging;
using Xunit;

namespace REIGN.Tests;

public class WebhookSecurityTests
{
    [Fact]
    public void Twilio_signature_round_trip_validates()
    {
        var token = "test-auth-token";
        var url = "https://example.com/api/sms/webhooks/twilio";
        var parameters = new Dictionary<string, string>
        {
            ["From"] = "+15555550123",
            ["To"] = "+15555550100",
            ["Body"] = "Book QV tomorrow 2pm",
            ["MessageSid"] = "SM123"
        };

        var signature = TwilioRequestValidator.ComputeSignature(token, url, parameters);
        Assert.True(TwilioRequestValidator.IsValid(token, url, parameters, signature));
        Assert.False(TwilioRequestValidator.IsValid(token, url, parameters, "AAAA"));
        Assert.False(TwilioRequestValidator.IsValid("other-token", url, parameters, signature));
    }

    [Fact]
    public void Twilio_signature_accepts_public_url_when_request_host_is_render_internal()
    {
        var token = "test-auth-token";
        var publicUrl = "https://reign-ai-2.onrender.com/api/sms/webhooks/twilio";
        var parameters = new Dictionary<string, string>
        {
            ["From"] = "+15555550123",
            ["Body"] = "Hi"
        };
        var signature = TwilioRequestValidator.ComputeSignature(token, publicUrl, parameters);

        var candidates = TwilioWebhookUrlResolver.Candidates(
            requestScheme: "http",
            requestHost: "[::]:10000",
            requestPath: "/api/sms/webhooks/twilio",
            requestQuery: "",
            webhookPublicUrl: null,
            publicBaseUrl: null,
            forwardedProto: "https",
            forwardedHost: "reign-ai-2.onrender.com",
            renderExternalUrl: "https://reign-ai-2.onrender.com");

        Assert.Contains(publicUrl, candidates);
        Assert.True(TwilioRequestValidator.IsValidAny(token, candidates, parameters, signature, out var matched));
        Assert.Equal(publicUrl, matched);
        Assert.False(TwilioRequestValidator.IsValid(token, "http://[::]:10000/api/sms/webhooks/twilio", parameters, signature));
    }

    [Fact]
    public void Twilio_webhook_url_resolver_appends_path_to_origin_only_base()
    {
        var candidates = TwilioWebhookUrlResolver.Candidates(
            requestScheme: "http",
            requestHost: "localhost:10000",
            requestPath: "/api/sms/webhooks/twilio",
            requestQuery: "",
            webhookPublicUrl: null,
            publicBaseUrl: "https://reign-ai-2.onrender.com");

        Assert.Contains("https://reign-ai-2.onrender.com/api/sms/webhooks/twilio", candidates);
        Assert.Contains("http://reign-ai-2.onrender.com/api/sms/webhooks/twilio", candidates);
        Assert.Contains("https://reign-ai-2.onrender.com/api/sms/webhooks/twilio/", candidates);
    }

    [Fact]
    public void Twilio_webhook_url_resolver_uses_configured_webhook_url_first()
    {
        var configured = "https://reign-ai-2.onrender.com/api/sms/incoming";
        var candidates = TwilioWebhookUrlResolver.Candidates(
            requestScheme: "http",
            requestHost: "[::]:10000",
            requestPath: "/api/sms/incoming",
            requestQuery: "",
            webhookPublicUrl: configured,
            publicBaseUrl: "https://other.example");

        Assert.Equal(configured, candidates[0]);
        Assert.Contains("https://reign-ai-2.onrender.com/api/sms/webhooks/twilio", candidates);
    }

    [Fact]
    public void Twilio_signature_accepts_incoming_path_when_request_host_is_render_internal()
    {
        var token = "test-auth-token";
        var publicUrl = "https://reign-ai-2.onrender.com/api/sms/incoming";
        var parameters = new Dictionary<string, string>
        {
            ["From"] = "+15555550123",
            ["To"] = "+15555550100",
            ["Body"] = "Book QV tomorrow 2pm",
            ["MessageSid"] = "SM123"
        };
        var signature = TwilioRequestValidator.ComputeSignature(token, publicUrl, parameters);

        var candidates = TwilioWebhookUrlResolver.Candidates(
            requestScheme: "http",
            requestHost: "[::]:10000",
            requestPath: "/api/sms/incoming",
            requestQuery: "",
            webhookPublicUrl: "https://reign-ai-2.onrender.com/api/sms/webhooks/twilio",
            publicBaseUrl: null,
            forwardedProto: "https",
            forwardedHost: "reign-ai-2.onrender.com",
            renderExternalUrl: "https://reign-ai-2.onrender.com");

        Assert.Contains(publicUrl, candidates);
        Assert.True(TwilioRequestValidator.IsValidAny(token, candidates, parameters, signature, out var matched));
        Assert.Equal(publicUrl, matched);
    }

    [Fact]
    public void Twilio_webhook_url_resolver_default_path_is_incoming()
    {
        var candidates = TwilioWebhookUrlResolver.Candidates(
            requestScheme: "https",
            requestHost: "reign-ai-2.onrender.com",
            requestPath: null,
            requestQuery: "",
            webhookPublicUrl: null,
            publicBaseUrl: "https://reign-ai-2.onrender.com");

        Assert.Contains("https://reign-ai-2.onrender.com/api/sms/incoming", candidates);
        Assert.Contains("https://reign-ai-2.onrender.com/api/sms/webhooks/twilio", candidates);
    }

    [Fact]
    public void SmsGate_hmac_validates_raw_body_plus_timestamp()
    {
        var key = "smsgate-signing-key";
        var body = """{"event":"sms:received","payload":{"sender":"+15555550123","message":"Book QV","recipient":"+15555550100"}}""";
        var timestamp = "1700000000";
        var signature = SmsGateWebhookValidator.ComputeSignature(key, body, timestamp);
        var now = DateTimeOffset.FromUnixTimeSeconds(1700000000);

        Assert.True(SmsGateWebhookValidator.IsValid(key, body, timestamp, signature, now));
        Assert.False(SmsGateWebhookValidator.IsValid(key, body, timestamp, "deadbeef", now));
        Assert.False(SmsGateWebhookValidator.IsValid("other-key", body, timestamp, signature, now));
        Assert.False(SmsGateWebhookValidator.IsValid(key, body, "100", signature, now));
    }

    [Fact]
    public void SmsGate_parser_reads_received_and_ignores_other_events()
    {
        var received = SmsGateWebhookValidator.TryParseReceived(
            """{"event":"sms:received","id":"wh1","payload":{"messageId":"m1","sender":"+15555550123","recipient":"+15555550100","message":"Hi Miss Reign"}}""");
        Assert.NotNull(received);
        Assert.Equal("+15555550123", received!.From);
        Assert.Equal("+15555550100", received.To);
        Assert.Equal("Hi Miss Reign", received.Body);
        Assert.Equal("m1", received.ProviderMessageId);
        Assert.Equal("SmsGate", received.Provider);

        var legacy = SmsGateWebhookValidator.TryParseReceived(
            """{"event":"sms:received","payload":{"phoneNumber":"+15555550999","message":"QV"}}""");
        Assert.NotNull(legacy);
        Assert.Equal("+15555550999", legacy!.From);

        Assert.Null(SmsGateWebhookValidator.TryParseReceived(
            """{"event":"sms:sent","payload":{"sender":"+15555550100","recipient":"+15555550123"}}"""));
    }

    [Fact]
    public void Vonage_jwt_validates_signature_and_payload_hash()
    {
        var secret = "vonage-signature-secret-32-bytes-min";
        var rawBody = """{"from":"15555550123","to":"15555550100","text":"hello"}""";
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawBody))).ToLowerInvariant();
        var jwt = VonageWebhookValidator.CreateHs256Jwt(secret, new Dictionary<string, object>
        {
            ["iat"] = 1700000000,
            ["iss"] = "Vonage",
            ["payload_hash"] = hash
        });

        Assert.True(VonageWebhookValidator.TryValidateJwt(secret, "Bearer " + jwt, rawBody));
        Assert.False(VonageWebhookValidator.TryValidateJwt(secret, "Bearer " + jwt, """{"tampered":true}"""));
        Assert.False(VonageWebhookValidator.TryValidateJwt("wrong-secret", "Bearer " + jwt, rawBody));
    }

    [Fact]
    public void Twilio_webhook_action_listens_on_incoming_and_legacy_form_post()
    {
        var method = typeof(REIGN.API.Controllers.SmsWebhookController).GetMethod(
            nameof(REIGN.API.Controllers.SmsWebhookController.Twilio));
        Assert.NotNull(method);
        var posts = method!.GetCustomAttributes(typeof(Microsoft.AspNetCore.Mvc.HttpPostAttribute), false)
            .Cast<Microsoft.AspNetCore.Mvc.HttpPostAttribute>()
            .Select(a => a.Template)
            .ToArray();
        Assert.Contains("twilio", posts);
        Assert.Contains("/api/sms/incoming", posts);
        Assert.Contains(
            method.GetCustomAttributes(typeof(Microsoft.AspNetCore.Mvc.ConsumesAttribute), false)
                .Cast<Microsoft.AspNetCore.Mvc.ConsumesAttribute>(),
            a => a.ContentTypes.Contains("application/x-www-form-urlencoded"));
    }
}
