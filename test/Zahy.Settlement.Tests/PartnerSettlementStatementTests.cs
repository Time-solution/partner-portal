using System;
using System.Linq;
using Shouldly;
using Xunit;

namespace Zahy.Settlement;

/// <summary>
/// DIRECTION 2 — PARTNER SETTLEMENT STATEMENT (Zahy owes the partner). A SEPARATE output from the
/// invoice — never merged. Reads owed/disbursed/remaining from DisbursementStatus and lists the orders
/// making up the payable; recomputes no money. BETA-stamped. Anchored to principal 70→100 (payable 70).
/// </summary>
public class PartnerSettlementStatementTests
{
    private const decimal Vat = 0.15m;
    private static readonly SettlementPeriod Period = SettlementPeriod.Of(2026, 6);
    private static readonly Guid Partner = Guid.NewGuid();
    private static readonly Guid Merchant = Guid.NewGuid();
    private static readonly DateTime At = new(2026, 6, 30, 0, 0, 0, DateTimeKind.Utc);

    private static Money Incl(decimal amount) => Money.Of(amount, SettlementConsts.DefaultCurrency, vatInclusive: true);

    private static (PostingResult[] entries, DisbursementStatusReport status) BuildDisbursed(bool release)
    {
        var entries = new[]
        {
            SettlementPostingTemplates.Principal(Incl(100.00m), Incl(70.00m), Vat).Tag(Partner, Merchant, Period, "ORD-1"),
        };
        var payments = new[] { Payment.Record(Guid.NewGuid(), "ORD-1", PaymentPayer.Merchant, Merchant, Incl(100m), At) };

        var match = Reconciliation.Match(Partner, Period, entries, payments);
        var batch = ReconciliationBatch.Open(Guid.NewGuid(), Partner, Period);
        batch.Reconcile(match, "accountant", At);

        var ctx = DisbursementContext.From(match, batch);
        var disb = Disbursements.CreateOrGet(Guid.NewGuid(), Partner, Period, Incl(70m), "DISB-1", At, ctx, Array.Empty<Disbursement>());
        if (release)
        {
            disb.Release("platform-admin", true, reconciledBy: batch.ReconciledBy!, At);
        }

        var status = Disbursements.Status(Partner, Period, ctx, new[] { disb });
        return (entries, status);
    }

    [Fact]
    public void Statement_Reads_Owed_Disbursed_Remaining_From_Disbursement_Status()
    {
        var (entries, status) = BuildDisbursed(release: true);

        var statement = PartnerSettlementStatement.Build(Partner, Period, entries, status);

        statement.OwedToPartner.Amount.ShouldBe(70.00m);
        statement.DisbursedToDate.Amount.ShouldBe(70.00m);
        statement.RemainingToDisburse.Amount.ShouldBe(0m);
        statement.IsReconciled.ShouldBeTrue();
        statement.State.ShouldBe(DisbursementPositionState.FullyDisbursed);
        statement.IsBeta.ShouldBeTrue();
        statement.BetaLabel.ShouldBe(SettlementInvoiceConsts.BetaLabel);
    }

    [Fact]
    public void Statement_Drill_Lists_The_Orders_Making_Up_The_Payable()
    {
        var (entries, status) = BuildDisbursed(release: false);

        var statement = PartnerSettlementStatement.Build(Partner, Period, entries, status);

        var line = statement.Lines.ShouldHaveSingleItem();
        line.OrderRef.ShouldBe("ORD-1");
        line.Amount.Amount.ShouldBe(70.00m); // net 2100 contribution
    }

    [Fact]
    public void Invoice_And_Statement_Are_Distinct_Objects_Never_Merged()
    {
        var (entries, status) = BuildDisbursed(release: true);
        var statement = PartnerSettlementStatement.Build(Partner, Period, entries, status);

        var invoice = InvoiceReader.Build(
            InvoiceEntityType.Partner, Partner, "CHEFZ", Period,
            new InvoiceLineInput[] { new PerTransactionLineInput("Aggregator", Incl(1.00m), 5) },
            Array.Empty<Payment>());

        // Two different report types — a receivable invoice is never the payable statement.
        invoice.GetType().ShouldNotBe(statement.GetType());
        typeof(InvoiceReport).ShouldNotBe(typeof(PartnerSettlementStatementReport));
        ((object)invoice).ShouldNotBeSameAs(statement);
    }

    [Fact]
    public void Statement_Reads_Existing_Read_Models_Without_Recompute()
    {
        var (entries, status) = BuildDisbursed(release: true);
        var statement = PartnerSettlementStatement.Build(Partner, Period, entries, status);

        // owedToPartner equals the partner statement payable (net 2100) — read, not recomputed.
        var partnerStatement = SettlementReports.Partner(entries, Partner, Period);
        statement.OwedToPartner.Amount.ShouldBe(partnerStatement.TotalPayable.Amount);

        // Building these read models posts nothing: the trial balance over the orders still nets zero.
        SettlementReports.TrialBalanceFor(entries, Period).Net.ShouldBe(0m);
    }

    [Fact]
    public void Flags_Stay_Off()
    {
        new SettlementEngineOptions().DisbursementEnabled.ShouldBeFalse();
        new SettlementEngineOptions().PostingEnabled.ShouldBeFalse();
    }
}
