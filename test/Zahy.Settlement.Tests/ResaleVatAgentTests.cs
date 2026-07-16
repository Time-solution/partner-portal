using Shouldly;
using Volo.Abp;
using Xunit;

namespace Zahy.Settlement;

/// <summary>
/// AF3 quarantine contract: the production VAT calculator HARD-REJECTS Agent treatment (KSA is
/// Principal-only) so no config/book wire can silently reach margin-only VAT; the Agent arithmetic is
/// preserved and still regression-covered through the explicit test-only verification entry.
/// Shipping 10→15: AGENT would tax only the commission (spread 5) ⇒ 0.65 / margin 4.35, vs PRINCIPAL
/// 0.66 / 4.34 — kept as the reason the two treatments must never be silently interchangeable.
/// </summary>
public class ResaleVatAgentTests
{
    private const decimal Rate = 0.15m;
    private static readonly ResaleVatCalculator Calc = new();

    private static CostMarkupLine Line(decimal buy, decimal sell) =>
        CostMarkupLine.Of(Money.Of(buy, vatInclusive: true), Money.Of(sell, vatInclusive: true));

    [Fact]
    public void Production_Compute_Rejects_Agent_Treatment_035()
    {
        Should.Throw<BusinessException>(() => Calc.Compute(Line(10m, 15m), Rate, VatTreatment.Agent))
            .Code.ShouldBe(SettlementVatErrorCodes.AgentTreatmentNotSupportedInKsa);
    }

    [Fact]
    public void Agent_Math_Preserved_Via_Test_Only_Entry_And_Differs_From_Principal()
    {
        var agent = ResaleVatCalculator.ComputeAgentForVerification(Line(10m, 15m), Rate);

        agent.InputVat.Amount.ShouldBe(0m);          // agent never reclaims input VAT
        agent.OutputVat.Amount.ShouldBe(0.65m);      // VAT on the gross spread (5)
        agent.Margin.Amount.ShouldBe(4.35m);
        agent.NetVatToZatca.Amount.ShouldBe(0.65m);

        var principal = Calc.Compute(Line(10m, 15m), Rate, VatTreatment.Principal);
        agent.NetVatToZatca.Amount.ShouldNotBe(principal.NetVatToZatca.Amount); // 0.65 vs 0.66
    }
}
