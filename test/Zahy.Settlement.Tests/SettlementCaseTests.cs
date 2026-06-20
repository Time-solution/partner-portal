using System;
using System.Linq;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace Zahy.Settlement;

public class SettlementCaseTests
{
    private static readonly DateTime T = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Start_Begins_In_Collected_With_Initial_History()
    {
        var c = SettlementCase.Start(Guid.NewGuid(), SettlementBook.Marketplace, Guid.NewGuid(), "txn-1", T);

        c.State.ShouldBe(SettlementCaseState.Collected);
        c.Book.ShouldBe(SettlementBook.Marketplace);
        c.History.Count.ShouldBe(1);
        c.History[0].To.ShouldBe(SettlementCaseState.Collected);
        c.History[0].From.ShouldBeNull();
    }

    [Fact]
    public void Advance_Walks_The_Full_Lifecycle_Then_Rejects_Further_Moves()
    {
        var c = SettlementCase.Start(Guid.NewGuid(), SettlementBook.Marketplace, Guid.NewGuid(), "txn-1", T);

        foreach (var _ in Enumerable.Range(0, 5))
        {
            c.Advance(T);
        }

        c.State.ShouldBe(SettlementCaseState.Reconciled);
        c.History.Count.ShouldBe(6); // Collected + 5 transitions

        Should.Throw<BusinessException>(() => c.Advance(T))
            .Code.ShouldBe(SettlementCaseErrorCodes.IllegalStateTransition);
    }

    [Fact]
    public void Illegal_Skip_Transition_Is_Rejected()
    {
        var c = SettlementCase.Start(Guid.NewGuid(), SettlementBook.Integration, Guid.NewGuid(), "txn-1", T);

        Should.Throw<BusinessException>(() => c.TransitionTo(SettlementCaseState.Invoiced, T))
            .Code.ShouldBe(SettlementCaseErrorCodes.IllegalStateTransition);

        c.State.ShouldBe(SettlementCaseState.Collected); // unchanged
    }

    [Fact]
    public void Empty_External_Transaction_Id_Is_Rejected()
    {
        Should.Throw<BusinessException>(() =>
                SettlementCase.Start(Guid.NewGuid(), SettlementBook.Marketplace, Guid.NewGuid(), "   ", T))
            .Code.ShouldBe(SettlementCaseErrorCodes.EmptyExternalTransactionId);
    }

    [Fact]
    public void Idempotency_Key_Is_Stable_Trimmed_And_Case_Insensitive()
    {
        var a = SettlementCase.BuildIdempotencyKey(SettlementBook.Marketplace, "  TXN-1 ");
        var b = SettlementCase.BuildIdempotencyKey(SettlementBook.Marketplace, "txn-1");
        a.ShouldBe(b);

        // Same external id in a different book is a different key — books are isolated.
        SettlementCase.BuildIdempotencyKey(SettlementBook.Integration, "txn-1").ShouldNotBe(b);
    }

    [Fact]
    public void Case_Exposes_Its_Idempotency_Key()
    {
        var c = SettlementCase.Start(Guid.NewGuid(), SettlementBook.Marketplace, Guid.NewGuid(), " Txn-9 ", T);
        c.ExternalTransactionId.ShouldBe("Txn-9");
        c.IdempotencyKey.ShouldBe(SettlementCase.BuildIdempotencyKey(SettlementBook.Marketplace, "txn-9"));
    }
}
