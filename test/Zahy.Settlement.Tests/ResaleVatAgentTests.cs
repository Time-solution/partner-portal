using Shouldly;
using Xunit;

namespace Zahy.Settlement;

/// <summary>
/// AGENT treatment differs from PRINCIPAL — proves the configurable flag actually changes the result.
/// Shipping 10→15: AGENT taxes only the commission (gross spread 5) ⇒ net-to-ZATCA 0.65 / margin 4.35,
/// versus PRINCIPAL 0.66 / 4.34. No input VAT is reclaimed under AGENT.
/// </summary>
public class ResaleVatAgentTests
{
    private const decimal Rate = 0.15m;
    private static readonly ResaleVatCalculator Calc = new();

    private static CostMarkupLine Line(decimal buy, decimal sell) =>
        CostMarkupLine.Of(Money.Of(buy, vatInclusive: true), Money.Of(sell, vatInclusive: true));

    [Fact]
    public void Shipping_Buy10_Sell15_Agent_Differs_From_Principal()
    {
        var agent = Calc.Compute(Line(10m, 15m), Rate, VatTreatment.Agent);

        agent.InputVat.Amount.ShouldBe(0m);          // agent never reclaims input VAT
        agent.OutputVat.Amount.ShouldBe(0.65m);      // VAT on the gross spread (5)
        agent.Margin.Amount.ShouldBe(4.35m);
        agent.NetVatToZatca.Amount.ShouldBe(0.65m);

        var principal = Calc.Compute(Line(10m, 15m), Rate, VatTreatment.Principal);
        agent.NetVatToZatca.Amount.ShouldNotBe(principal.NetVatToZatca.Amount); // 0.65 vs 0.66
    }
}
