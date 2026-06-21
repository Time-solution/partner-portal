using System;
using System.Linq;
using Shouldly;
using Xunit;

namespace Zahy.Settlement;

/// <summary>
/// DIRECTION 1 — INVOICE read model (entity owes Zahy). Reads existing read models and reuses VatMath
/// for the split; recomputes no money. Anchored to the mock numbers: 5 × 1.00 per-txn + 40.00
/// subscription = 45.00 incl (39.13 ex + 5.87 vat); proration 100.00 → 50.00 at day 15 of a 30-day month.
/// Every invoice is BETA-stamped (not a ZATCA tax invoice yet).
/// </summary>
public class InvoiceReportTests
{
    private static readonly SettlementPeriod Period = SettlementPeriod.Of(2026, 6); // June = 30 days
    private static readonly Guid MerchantId = Guid.NewGuid();

    private static Money Incl(decimal amount) => Money.Of(amount, SettlementConsts.DefaultCurrency, vatInclusive: true);

    private static Payment Pays(string againstRef, decimal amount) =>
        Payment.Record(Guid.NewGuid(), againstRef, PaymentPayer.Merchant, MerchantId, Incl(amount), DateTime.UtcNow);

    [Fact]
    public void Multi_Service_Invoice_Has_Two_Lines_And_Totals_45()
    {
        var lines = new InvoiceLineInput[]
        {
            new PerTransactionLineInput("Aggregator", Incl(1.00m), SuccessfulCount: 5),
            new SubscriptionLineInput("Platform", Incl(40.00m)),
        };

        var invoice = InvoiceReader.Build(
            InvoiceEntityType.Merchant, MerchantId, "CHEFZ-M1", Period, lines, Array.Empty<Payment>());

        invoice.Lines.Count.ShouldBe(2);
        invoice.TotalInclusive.Amount.ShouldBe(45.00m);
        invoice.TotalExVat.Amount.ShouldBe(39.13m);
        invoice.TotalVat.Amount.ShouldBe(5.87m);

        // The per-txn line is 5 × 1.00 = 5.00; the subscription line is the full 40.00.
        var perTxn = invoice.Lines.Single(l => l.BillingType == BillingType.PerTransaction);
        perTxn.AmountInclusive.Amount.ShouldBe(5.00m);
        var sub = invoice.Lines.Single(l => l.BillingType == BillingType.Subscription);
        sub.AmountInclusive.Amount.ShouldBe(40.00m);

        invoice.IsBeta.ShouldBeTrue();
        invoice.BetaLabel.ShouldBe(SettlementInvoiceConsts.BetaLabel);
    }

    [Fact]
    public void Invoice_Payment_Walks_Allocated_PartiallyPaid_Paid()
    {
        var lines = new InvoiceLineInput[]
        {
            new PerTransactionLineInput("Aggregator", Incl(1.00m), 5),
            new SubscriptionLineInput("Platform", Incl(40.00m)),
        };

        var number = SettlementInvoiceNumber.For("CHEFZ-M1", Period);

        var unpaid = InvoiceReader.Build(InvoiceEntityType.Merchant, MerchantId, "CHEFZ-M1", Period, lines, Array.Empty<Payment>());
        unpaid.State.ShouldBe(PaymentState.Allocated);
        unpaid.Remaining.Amount.ShouldBe(45.00m);

        var partial = InvoiceReader.Build(InvoiceEntityType.Merchant, MerchantId, "CHEFZ-M1", Period, lines, new[] { Pays(number, 20m) });
        partial.PaidToDate.Amount.ShouldBe(20.00m);
        partial.Remaining.Amount.ShouldBe(25.00m);
        partial.State.ShouldBe(PaymentState.PartiallyPaid);

        var paid = InvoiceReader.Build(InvoiceEntityType.Merchant, MerchantId, "CHEFZ-M1", Period, lines, new[] { Pays(number, 20m), Pays(number, 25m) });
        paid.Remaining.Amount.ShouldBe(0m);
        paid.State.ShouldBe(PaymentState.Paid);
    }

    [Fact]
    public void Subscription_Prorated_Day_15_Of_30_Is_Half()
    {
        var lines = new InvoiceLineInput[]
        {
            new SubscriptionLineInput("Platform", Incl(100.00m), ActiveDays: 15),
        };

        var invoice = InvoiceReader.Build(
            InvoiceEntityType.Merchant, MerchantId, "M1", Period, lines, Array.Empty<Payment>());

        var line = invoice.Lines.Single();
        line.AmountInclusive.Amount.ShouldBe(50.00m);
        line.ExVat.Amount.ShouldBe(43.48m);
        line.Vat.Amount.ShouldBe(6.52m);
        line.QtyOrBasis.ShouldBe("15/30 days");

        invoice.TotalInclusive.Amount.ShouldBe(50.00m);
    }

    [Fact]
    public void Full_Month_Subscription_Is_Not_Prorated()
    {
        var lines = new InvoiceLineInput[] { new SubscriptionLineInput("Platform", Incl(100.00m)) };

        var invoice = InvoiceReader.Build(
            InvoiceEntityType.Merchant, MerchantId, "M1", Period, lines, Array.Empty<Payment>());

        invoice.Lines.Single().AmountInclusive.Amount.ShouldBe(100.00m);
        invoice.Lines.Single().QtyOrBasis.ShouldBe("full month (30/30 days)");
    }

    [Fact]
    public void Invoice_Number_Is_Deterministic_And_Stable()
    {
        var a = SettlementInvoiceNumber.For("CHEFZ", Period);
        var b = SettlementInvoiceNumber.For("chefz", Period); // case-insensitive → same

        a.ShouldBe("ZH-202606-CHEFZ-01");
        b.ShouldBe(a);

        InvoiceReader.Build(InvoiceEntityType.Partner, Guid.NewGuid(), "CHEFZ", Period, Array.Empty<InvoiceLineInput>(), Array.Empty<Payment>())
            .InvoiceNumber.ShouldBe("ZH-202606-CHEFZ-01");
    }

    [Fact]
    public void Flags_Stay_Off()
    {
        new SettlementEngineOptions().PostingEnabled.ShouldBeFalse();
        new SettlementEngineOptions().DisbursementEnabled.ShouldBeFalse();
    }
}
