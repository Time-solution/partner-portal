using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Shouldly;
using Xunit;

namespace Zahy.Settlement;

public class SettlementWebhookIngestionTests
{
    private const string Secret = "partner-signing-secret";
    private const long Ts = 1_700_000_000;
    private static readonly DateTime At = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly Guid Partner = Guid.NewGuid();

    // ---- in-memory fakes (keep the pipeline host-free and deterministic) -------------------------
    private sealed class FakeCaseStore : ISettlementCaseStore
    {
        public readonly List<SettlementCase> Cases = new();
        public Task<SettlementCase?> FindByKeyAsync(SettlementBook book, string ext, CancellationToken ct = default) =>
            Task.FromResult(Cases.FirstOrDefault(c => c.Book == book && c.ExternalTransactionId == ext));
        public Task InsertAsync(SettlementCase c, CancellationToken ct = default) { Cases.Add(c); return Task.CompletedTask; }
        public Task UpdateAsync(SettlementCase c, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeEventStore : ISettlementEventStore
    {
        public readonly List<SettlementWebhookEvent> Events = new();
        public Task InsertAsync(SettlementWebhookEvent e, CancellationToken ct = default) { Events.Add(e); return Task.CompletedTask; }
        public Task<IReadOnlyList<SettlementWebhookEvent>> GetByCaseAsync(Guid caseId, CancellationToken ct = default) =>
            Task.FromResult((IReadOnlyList<SettlementWebhookEvent>)Events.Where(e => e.SettlementCaseId == caseId).ToList());
    }

    private static (SettlementWebhookIngestionService svc, FakeCaseStore cases, FakeEventStore events) Build()
    {
        var cases = new FakeCaseStore();
        var events = new FakeEventStore();
        var resolver = new SettlementFlowProfileResolver(new ISettlementFlowProfile[]
        {
            new AggregatorFlowProfile(), new ServiceFlowProfile()
        });
        var svc = new SettlementWebhookIngestionService(
            new HmacSettlementWebhookSignatureVerifier(), resolver, new SettlementAllocator(), cases, events);
        return (svc, cases, events);
    }

    private static InboundSettlementWebhook Webhook(string eventId, bool signed = true, bool tamper = false, bool partnerKnown = true)
    {
        const string payload = "{\"event\":\"collected\",\"total\":100}";
        var signature = Zahy.Webhooks.WebhookHmacSigner
            .Sign(Secret, payload, DateTimeOffset.FromUnixTimeSeconds(Ts)).Signature;

        return new InboundSettlementWebhook
        {
            Book = SettlementBook.Marketplace,
            PartnerId = Partner,
            PartnerKnown = partnerKnown,
            ExternalEventId = eventId,
            Payload = tamper ? payload + "X" : payload, // tamper invalidates the signature
            SigningSecret = Secret,
            Signature = signed ? signature : null,
            UnixTimestamp = Ts,
            ReceivedAt = At,
            Allocation = new SettlementAllocationInput
            {
                Book = SettlementBook.Marketplace,
                PartnerId = Partner,
                ExternalTransactionId = eventId,
                CollectedTotal = Money.Of(100m),
                MerchantPayout = Money.Of(80m),
                PlatformCommissionInclusive = Money.Of(15m, vatInclusive: true),
                DeliveryCost = Money.Of(5m),
                VatRate = 0.15m
            }
        };
    }

    [Fact]
    public async Task Unsigned_Webhook_Is_Rejected_And_Logged_No_Case()
    {
        var (svc, cases, events) = Build();

        var result = await svc.ProcessAsync(Webhook("evt-1", signed: false));

        result.Outcome.ShouldBe(SettlementEventOutcome.Rejected);
        result.SignatureStatus.ShouldBe(WebhookSignatureStatus.Missing);
        cases.Cases.ShouldBeEmpty();
        events.Events.ShouldContain(e => e.Outcome == SettlementEventOutcome.Rejected);
    }

    [Fact]
    public async Task Tampered_Payload_Fails_Signature_And_Is_Rejected()
    {
        var (svc, cases, _) = Build();

        var result = await svc.ProcessAsync(Webhook("evt-1", tamper: true));

        result.Outcome.ShouldBe(SettlementEventOutcome.Rejected);
        result.SignatureStatus.ShouldBe(WebhookSignatureStatus.Invalid);
        cases.Cases.ShouldBeEmpty();
    }

    [Fact]
    public async Task Unknown_Partner_Is_Quarantined_Never_Processed()
    {
        var (svc, cases, events) = Build();

        var result = await svc.ProcessAsync(Webhook("evt-1", partnerKnown: false));

        result.Outcome.ShouldBe(SettlementEventOutcome.Quarantined);
        cases.Cases.ShouldBeEmpty();
        events.Events.ShouldContain(e => e.Outcome == SettlementEventOutcome.Quarantined);
    }

    [Fact]
    public async Task Acknowledge_Accepts_Valid_And_Refuses_Unsigned()
    {
        var (svc, _, _) = Build();

        (await svc.AcknowledgeAsync(Webhook("evt-1"))).Accepted.ShouldBeTrue();
        (await svc.AcknowledgeAsync(Webhook("evt-2", signed: false))).Accepted.ShouldBeFalse();
    }

    [Fact]
    public async Task Replay_Of_Same_Event_Posts_No_Second_Journal_Or_Transition()
    {
        var (svc, cases, events) = Build();

        var first = await svc.ProcessAsync(Webhook("evt-1"));
        var second = await svc.ProcessAsync(Webhook("evt-1")); // retry-safe

        first.Outcome.ShouldBe(SettlementEventOutcome.Allocated);
        second.Outcome.ShouldBe(SettlementEventOutcome.DuplicateIgnored);
        cases.Cases.Count.ShouldBe(1); // no second case
        events.Events.Count(e => e.Outcome == SettlementEventOutcome.Allocated).ShouldBe(1); // one allocation only
        second.SettlementCaseId.ShouldBe(first.SettlementCaseId);
    }

    [Fact]
    public async Task Worked_Example_Collected_100_Allocates_Balanced_Journal_And_Explains()
    {
        var (svc, cases, events) = Build();
        var result = await svc.ProcessAsync(Webhook("evt-collected-100"));

        result.Outcome.ShouldBe(SettlementEventOutcome.Allocated);
        result.State.ShouldBe(SettlementCaseState.Allocated); // Collected → Allocated

        var explainService = new SettlementExplainService(cases, events);
        var dto = await explainService.ExplainAsync(SettlementBook.Marketplace, "evt-collected-100");

        dto.ShouldNotBeNull();
        dto!.State.ShouldBe(SettlementCaseState.Allocated);
        dto.Book.ShouldBe(SettlementBook.Marketplace);

        var a = dto.Allocation.ShouldNotBeNull();
        a!.TotalDebits.ShouldBe(100m);
        a.TotalCredits.ShouldBe(100m);        // balanced journal
        a.MerchantPayout.ShouldBe(80m);       // recorded as money owed
        a.PlatformCommissionNet.ShouldBe(13.04m);
        a.VatOutput.ShouldBe(1.96m);
        a.NetVatToZatca.ShouldBe(1.96m);
        a.Legs.ShouldContain(l => l.Account == "AggregatorClearing" && l.Direction == "Debit" && l.Amount == 100m);
        a.Legs.ShouldContain(l => l.Account == "MerchantPayable" && l.Direction == "Credit" && l.Amount == 80m);

        // The explain joins back to the triggering webhook event.
        dto.Events.ShouldContain(e => e.Outcome == SettlementEventOutcome.Allocated && e.ResultingState == SettlementCaseState.Allocated);
    }
}
