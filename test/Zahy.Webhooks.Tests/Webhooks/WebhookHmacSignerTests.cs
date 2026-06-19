using Shouldly;
using Xunit;

namespace Zahy.Webhooks;

public class WebhookHmacSignerTests
{
    [Fact]
    public void Should_Sign_And_Verify_Payload_With_Known_Secret()
    {
        const string secret = "test-signing-secret";
        const string payload = """{"orderId":"123","totalAmount":100}""";
        var timestamp = 1_700_000_000L;

        var signed = WebhookHmacSigner.Sign(secret, payload, System.DateTimeOffset.FromUnixTimeSeconds(timestamp));

        signed.UnixTimestamp.ShouldBe(timestamp);
        WebhookHmacSigner.Verify(secret, payload, signed.UnixTimestamp, signed.Signature).ShouldBeTrue();
    }

    [Fact]
    public void Should_Reject_Tampered_Payload()
    {
        const string secret = "test-signing-secret";
        const string payload = """{"orderId":"123"}""";
        var signed = WebhookHmacSigner.Sign(secret, payload);

        WebhookHmacSigner.Verify(secret, """{"orderId":"456"}""", signed.UnixTimestamp, signed.Signature)
            .ShouldBeFalse();
    }
}
