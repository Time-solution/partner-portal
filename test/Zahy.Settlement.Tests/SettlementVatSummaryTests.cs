using Shouldly;
using Xunit;

namespace Zahy.Settlement;

public class SettlementVatSummaryTests
{
    [Fact]
    public void Normal_Period_Reports_Output_Minus_Input()
    {
        var summary = SettlementVatSummary.Of(Money.Of(13.04m), Money.Of(9.13m));

        summary.IsPayoutDominant.ShouldBeFalse();
        summary.InvoiceVatDue.Amount.ShouldBe(3.91m);
        summary.CarriedInputVatCredit.Amount.ShouldBe(0m);
    }

    [Fact]
    public void Payout_Dominant_Period_Never_Reports_Negative_Vat()
    {
        // Input VAT (9.13) exceeds output VAT (1.96): raw net would be −7.17.
        var summary = SettlementVatSummary.Of(Money.Of(1.96m), Money.Of(9.13m));

        summary.RawNetVat.Amount.ShouldBe(-7.17m);       // raw figure can be negative
        summary.IsPayoutDominant.ShouldBeTrue();
        summary.InvoiceVatDue.Amount.ShouldBe(0m);       // GUARD: tax invoice never shows negative VAT
        summary.CarriedInputVatCredit.Amount.ShouldBe(7.17m); // excess carried as a reclaim
    }
}
