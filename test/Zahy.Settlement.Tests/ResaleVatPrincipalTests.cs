using Shouldly;
using Xunit;

namespace Zahy.Settlement;

/// <summary>
/// The CTO-confirmed PRINCIPAL worked examples (VAT 15%, inclusive prices, round-per-line).
/// These exact numbers are the acceptance criteria for the VAT engine.
/// </summary>
public class ResaleVatPrincipalTests
{
    private const decimal Rate = 0.15m;
    private static readonly ResaleVatCalculator Calc = new();

    private static CostMarkupLine Line(decimal buy, decimal sell) =>
        CostMarkupLine.Of(
            Money.Of(buy, vatInclusive: true),
            Money.Of(sell, vatInclusive: true));

    [Fact]
    public void Partner_Service_Buy70_Sell100_Principal()
    {
        var r = Calc.Compute(Line(70m, 100m), Rate, VatTreatment.Principal);

        r.NetBuy.Amount.ShouldBe(60.87m);
        r.InputVat.Amount.ShouldBe(9.13m);
        r.NetSell.Amount.ShouldBe(86.96m);
        r.OutputVat.Amount.ShouldBe(13.04m);
        r.Margin.Amount.ShouldBe(26.09m);
        r.NetVatToZatca.Amount.ShouldBe(3.91m);
    }

    [Fact]
    public void Shipping_Buy10_Sell15_Principal()
    {
        var r = Calc.Compute(Line(10m, 15m), Rate, VatTreatment.Principal);

        r.NetBuy.Amount.ShouldBe(8.70m);
        r.InputVat.Amount.ShouldBe(1.30m);
        r.NetSell.Amount.ShouldBe(13.04m);
        r.OutputVat.Amount.ShouldBe(1.96m);
        // Round-per-line: net-to-ZATCA = 1.96 − 1.30 = 0.66 (NOT 0.65), margin = 13.04 − 8.70 = 4.34.
        r.NetVatToZatca.Amount.ShouldBe(0.66m);
        r.Margin.Amount.ShouldBe(4.34m);
    }
}
