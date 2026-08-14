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
}
