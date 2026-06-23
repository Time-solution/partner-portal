using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Shouldly;
using Volo.Abp.Timing;
using Xunit;
using Zahy.Webhooks;

namespace Zahy.Connectors;

public class ConnectorInboundVerifierTests
{
    private const string SecretRef = "vault://mock-aggregator";
    private const string Secret = "connector-signing-secret";
    private const string Payload = """{"externalOrderId":"order-1","total":113.5}""";
    private const long SignedAt = 1_700_000_000L;
    private const string Source = "connector:aggregator:mock-aggregator";

    private sealed class FixedClock : IClock
    {
        private readonly DateTime _now;
        public FixedClock(DateTime now) => _now = now;
        public DateTime Now => _now;
        public DateTimeKind Kind => DateTimeKind.Utc;
        public bool SupportsMultipleTimezone => false;
        public DateTime Normalize(DateTime dateTime) => dateTime;
        public DateTime ConvertToUtc(DateTime dateTime) => DateTime.SpecifyKind(dateTime, DateTimeKind.Utc);
        public DateTime ConvertToUserTime(DateTime dateTime) => dateTime;
        public DateTimeOffset ConvertToUserTime(DateTimeOffset dateTimeOffset) => dateTimeOffset;
    }

    private static ConnectorInboundVerifier Build(
        long nowUnix = SignedAt,
        int window = 300,
        IConnectorReplayGuard? guard = null,
        Dictionary<string, string>? secrets = null)
    {
        var options = Options.Create(new ConnectorInboundSecurityOptions
        {
            VerifySignature = true,
            FreshnessWindowSeconds = window,
            Secrets = secrets ?? new Dictionary<string, string> { [SecretRef] = Secret }
        });

        return new ConnectorInboundVerifier(
            new ConfigConnectorSecretResolver(options),
            guard ?? new InMemoryConnectorReplayGuard(),
            new FixedClock(DateTimeOffset.FromUnixTimeSeconds(nowUnix).UtcDateTime),
            options);
    }

    private static ReceiveOrderRequest Request(
        string externalId = "order-1",
        string? nonce = null,
        bool signed = true,
        string signingSecret = Secret,
        bool tamper = false,
        long ts = SignedAt)
    {
        var sentBody = tamper ? Payload + "X" : Payload;
        var signature = signed
            ? WebhookHmacSigner.Sign(signingSecret, Payload, DateTimeOffset.FromUnixTimeSeconds(ts)).Signature
            : null;

        return new ReceiveOrderRequest
        {
            Intent = ReceiveOrderIntent.InboundFromPartner,
            ExternalOrderId = externalId,
            RawPayload = sentBody,
            Signature = signature,
            UnixTimestamp = ts,
            Nonce = nonce
        };
    }

    [Fact]
    public async Task Valid_Signed_Request_Passes()
    {
        var status = await Build().VerifyAsync(SecretRef, Source, Request());
        status.ShouldBe(ConnectorSignatureStatus.Valid);
    }

    [Fact]
    public async Task Secret_Is_Resolved_Per_Connector_From_Its_SecretReference()
    {
        var verifier = Build();

        // The same signed request verifies under the registration's reference …
        (await verifier.VerifyAsync(SecretRef, Source, Request("order-a", nonce: "n-a")))
            .ShouldBe(ConnectorSignatureStatus.Valid);

        // … but an unknown reference resolves no secret, so it cannot be verified.
        (await verifier.VerifyAsync("vault://unknown-connector", Source, Request("order-b", nonce: "n-b")))
            .ShouldBe(ConnectorSignatureStatus.NoSecret);
    }

    [Fact]
    public async Task Missing_Signature_Is_Missing()
    {
        var status = await Build().VerifyAsync(SecretRef, Source, Request(signed: false));
        status.ShouldBe(ConnectorSignatureStatus.Missing);
    }

    [Fact]
    public async Task Caller_Supplied_Foreign_Secret_Is_Not_Trusted()
    {
        // Caller signs with their own secret; the verifier only trusts the registration's secret.
        var status = await Build().VerifyAsync(SecretRef, Source, Request(signingSecret: "attacker-secret"));
        status.ShouldBe(ConnectorSignatureStatus.Invalid);
    }

    [Fact]
    public async Task Tampered_Body_Is_Invalid()
    {
        var status = await Build().VerifyAsync(SecretRef, Source, Request(tamper: true));
        status.ShouldBe(ConnectorSignatureStatus.Invalid);
    }

    [Fact]
    public async Task Stale_Timestamp_Is_Rejected()
    {
        var status = await Build(nowUnix: SignedAt + 10_000).VerifyAsync(SecretRef, Source, Request());
        status.ShouldBe(ConnectorSignatureStatus.Stale);
    }

    [Fact]
    public async Task Replayed_Nonce_Is_Rejected()
    {
        var guard = new InMemoryConnectorReplayGuard();
        var verifier = Build(guard: guard);

        var first = await verifier.VerifyAsync(SecretRef, Source, Request(nonce: "replay-nonce"));
        var second = await verifier.VerifyAsync(SecretRef, Source, Request(nonce: "replay-nonce"));

        first.ShouldBe(ConnectorSignatureStatus.Valid);
        second.ShouldBe(ConnectorSignatureStatus.Replay);
    }

    [Fact]
    public async Task Nonce_Falls_Back_To_ExternalOrderId_When_Absent()
    {
        var guard = new InMemoryConnectorReplayGuard();
        var verifier = Build(guard: guard);

        (await verifier.VerifyAsync(SecretRef, Source, Request("dup-order")))
            .ShouldBe(ConnectorSignatureStatus.Valid);
        (await verifier.VerifyAsync(SecretRef, Source, Request("dup-order")))
            .ShouldBe(ConnectorSignatureStatus.Replay);
    }
}
