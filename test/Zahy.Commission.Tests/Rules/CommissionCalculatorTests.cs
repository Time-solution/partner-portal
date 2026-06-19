using Shouldly;
using Xunit;
using Zahy.PartnerPlatform.Partners;

namespace Zahy.Commission;

public class CommissionCalculatorTests
{
    private readonly CommissionCalculator _calculator = new();

    [Fact]
    public void Should_Compute_Flat_Fee_Only()
    {
        var result = _calculator.Compute(
            250m,
            new CommissionBasisDefinition { FlatFee = 7.50m });

        result.ComputedCommission.ShouldBe(7.50m);
        result.RawComponentTotal.ShouldBe(7.50m);
    }

    [Fact]
    public void Should_Compute_Percentage_Only()
    {
        var result = _calculator.Compute(
            100m,
            new CommissionBasisDefinition { PercentageRate = 0.125m });

        result.ComputedCommission.ShouldBe(12.50m);
    }

    [Fact]
    public void Should_Compute_Tiered_Bracket()
    {
        var result = _calculator.Compute(
            600m,
            new CommissionBasisDefinition
            {
                Tiered = new TieredCommissionDefinition
                {
                    Mode = TieredCommissionMode.Bracket,
                    Tiers =
                    [
                        new CommissionTier { FromAmount = 0m, ToAmount = 500m, Rate = 0.05m },
                        new CommissionTier { FromAmount = 500m, Rate = 0.03m }
                    ]
                }
            });

        result.ComputedCommission.ShouldBe(18.00m);
    }

    [Fact]
    public void Should_Apply_Bracket_Tier_To_Whole_Basis_Not_Marginal()
    {
        var basisDefinition = new CommissionBasisDefinition
        {
            Tiered = new TieredCommissionDefinition
            {
                Mode = TieredCommissionMode.Bracket,
                Tiers =
                [
                    new CommissionTier { FromAmount = 0m, ToAmount = 500m, Rate = 0.05m },
                    new CommissionTier { FromAmount = 500m, Rate = 0.03m }
                ]
            }
        };

        var bracketResult = _calculator.Compute(600m, basisDefinition);
        bracketResult.ComputedCommission.ShouldBe(18.00m);

        var marginalWouldBe = CommissionMoney.RoundCommission((500m * 0.05m) + (100m * 0.03m));
        marginalWouldBe.ShouldBe(28.00m);
        bracketResult.ComputedCommission.ShouldNotBe(marginalWouldBe);
    }

    [Fact]
    public void Should_Combine_Flat_And_Percentage()
    {
        var result = _calculator.Compute(
            100m,
            new CommissionBasisDefinition
            {
                FlatFee = 5m,
                PercentageRate = 0.03m
            });

        result.ComputedCommission.ShouldBe(8.00m);
    }

    [Fact]
    public void Should_Enforce_Min_Commission()
    {
        var result = _calculator.Compute(
            50m,
            new CommissionBasisDefinition
            {
                PercentageRate = 0.10m,
                MinCommission = 10m
            });

        result.RawComponentTotal.ShouldBe(5.00m);
        result.ComputedCommission.ShouldBe(10.00m);
    }

    [Fact]
    public void Should_Enforce_Max_And_Cap()
    {
        var maxResult = _calculator.Compute(
            1000m,
            new CommissionBasisDefinition
            {
                PercentageRate = 0.20m,
                MaxCommission = 150m
            });

        maxResult.ComputedCommission.ShouldBe(150.00m);

        var capResult = _calculator.Compute(
            1000m,
            new CommissionBasisDefinition
            {
                PercentageRate = 0.20m,
                CapCommission = 175m
            });

        capResult.ComputedCommission.ShouldBe(175.00m);
    }

    [Fact]
    public void Should_Round_Commission_Away_From_Zero()
    {
        var halfHalala = _calculator.Compute(
            100.05m,
            new CommissionBasisDefinition { PercentageRate = 0.10m });

        halfHalala.RawComponentTotal.ShouldBe(10.005m);
        halfHalala.ComputedCommission.ShouldBe(10.01m);

        var belowHalfHalala = _calculator.Compute(
            100.005m,
            new CommissionBasisDefinition { PercentageRate = 0.10m });

        belowHalfHalala.RawComponentTotal.ShouldBe(10.0005m);
        belowHalfHalala.ComputedCommission.ShouldBe(10.00m);
    }
}

public class CommissionBasisAmountResolverTests
{
    private readonly CommissionBasisAmountResolver _basisResolver = new();
    private readonly CommissionCalculator _calculator = new();

    [Fact]
    public void Should_Compute_On_Subtotal_Not_Total()
    {
        var orderAmounts = new CommissionOrderAmounts
        {
            Subtotal = 100m,
            TaxAmount = 15m,
            DeliveryFee = 20m,
            TotalAmount = 135m
        };

        var rule = CreateRule(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            CommissionScopeKind.PartnerType,
            CommissionFeeType.Sale,
            0.10m,
            scopePartnerType: PartnerType.Aggregator);

        var basis = _basisResolver.Resolve(orderAmounts, rule.ToSnapshot());
        basis.ShouldBe(100m);

        var result = _calculator.Compute(basis, rule.GetBasisDefinition());
        result.ComputedCommission.ShouldBe(10.00m);
        result.ComputedCommission.ShouldNotBe(13.50m);
    }

    [Fact]
    public void Should_Use_TotalAmount_Only_When_Explicitly_Configured_On_Rule()
    {
        var orderAmounts = new CommissionOrderAmounts
        {
            Subtotal = 100m,
            TaxAmount = 15m,
            DeliveryFee = 20m,
            TotalAmount = 135m
        };

        var rule = new CommissionRule(
            Guid.Parse("22222222-2222-2222-2222-222222222222"),
            "Total basis rule",
            CommissionDirection.PlatformEarns,
            CommissionTriggerType.Sale,
            CommissionScopeKind.PartnerType,
            new CommissionBasisDefinition { PercentageRate = 0.10m },
            DateTime.UtcNow.AddDays(-1),
            CommissionFeeType.Sale,
            basisAmountKind: CommissionBasisAmountKind.TotalAmount,
            scopePartnerType: PartnerType.Aggregator);

        var basis = _basisResolver.Resolve(orderAmounts, rule.ToSnapshot());
        basis.ShouldBe(135m);

        var result = _calculator.Compute(basis, rule.GetBasisDefinition());
        result.ComputedCommission.ShouldBe(13.50m);
    }

    private static CommissionRule CreateRule(
        Guid id,
        CommissionScopeKind scopeKind,
        CommissionFeeType feeType,
        decimal percentageRate,
        int priority = 0,
        Guid? scopePartnerId = null,
        PartnerType? scopePartnerType = null,
        string? scopeCategoryCode = null,
        string? scopeProductSku = null) =>
        new(
            id,
            $"Rule-{id}",
            CommissionDirection.PlatformEarns,
            CommissionTriggerType.Sale,
            scopeKind,
            new CommissionBasisDefinition { PercentageRate = percentageRate },
            DateTime.UtcNow.AddDays(-1),
            feeType,
            priority,
            scopePartnerId: scopePartnerId,
            scopePartnerType: scopePartnerType,
            scopeCategoryCode: scopeCategoryCode,
            scopeProductSku: scopeProductSku);
}

public class CommissionRuleWinnerResolverTests
{
    private readonly CommissionRuleWinnerResolver _resolver = new();

    [Fact]
    public void Overlapping_Rules_Do_Not_Double_Accrue_Same_Fee()
    {
        var partnerId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var partnerTypeRuleId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var partnerRuleId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");

        var partnerTypeRule = CreateRule(
            partnerTypeRuleId,
            CommissionScopeKind.PartnerType,
            CommissionFeeType.Sale,
            0.10m,
            scopePartnerType: PartnerType.Aggregator);

        var partnerRule = CreateRule(
            partnerRuleId,
            CommissionScopeKind.Partner,
            CommissionFeeType.Sale,
            0.15m,
            scopePartnerId: partnerId);

        var winners = _resolver.ResolveWinningRules([partnerTypeRule.ToSnapshot(), partnerRule.ToSnapshot()]);

        winners.Count.ShouldBe(1);
        winners.Single().Id.ShouldBe(partnerRuleId);
        winners.Single().BasisDefinition.PercentageRate.ShouldBe(0.15m);
    }

    [Fact]
    public void Distinct_Fee_Types_May_Both_Win_On_Same_Order()
    {
        var saleRule = CreateRule(
            Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"),
            CommissionScopeKind.PartnerType,
            CommissionFeeType.Sale,
            0.10m,
            scopePartnerType: PartnerType.Aggregator);

        var shipmentRule = CreateRule(
            Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee"),
            CommissionScopeKind.PartnerType,
            CommissionFeeType.Shipment,
            0.05m,
            scopePartnerType: PartnerType.ThreePL);

        var winners = _resolver.ResolveWinningRules([saleRule.ToSnapshot(), shipmentRule.ToSnapshot()]);

        winners.Count.ShouldBe(2);
        winners.Select(x => x.FeeType).ShouldBe([CommissionFeeType.Sale, CommissionFeeType.Shipment]);
    }

    [Fact]
    public void Should_Break_Ties_By_Priority_Then_Id_When_Scope_Is_Equal()
    {
        var lowerPriorityRule = CreateRule(
            Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff"),
            CommissionScopeKind.PartnerType,
            CommissionFeeType.Sale,
            0.08m,
            priority: 1,
            scopePartnerType: PartnerType.Aggregator);

        var higherPriorityRule = CreateRule(
            Guid.Parse("10101010-1010-1010-1010-101010101010"),
            CommissionScopeKind.PartnerType,
            CommissionFeeType.Sale,
            0.12m,
            priority: 5,
            scopePartnerType: PartnerType.Aggregator);

        var winners = _resolver.ResolveWinningRules([lowerPriorityRule.ToSnapshot(), higherPriorityRule.ToSnapshot()]);

        winners.Single().Id.ShouldBe(higherPriorityRule.Id);
    }

    private static CommissionRule CreateRule(
        Guid id,
        CommissionScopeKind scopeKind,
        CommissionFeeType feeType,
        decimal percentageRate,
        int priority = 0,
        Guid? scopePartnerId = null,
        PartnerType? scopePartnerType = null) =>
        new(
            id,
            $"Rule-{id}",
            CommissionDirection.PlatformEarns,
            CommissionTriggerType.Sale,
            scopeKind,
            new CommissionBasisDefinition { PercentageRate = percentageRate },
            DateTime.UtcNow.AddDays(-1),
            feeType,
            priority,
            scopePartnerId: scopePartnerId,
            scopePartnerType: scopePartnerType);
}
