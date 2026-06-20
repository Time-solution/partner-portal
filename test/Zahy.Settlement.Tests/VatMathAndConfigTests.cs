using Shouldly;
using Volo.Abp;
using Xunit;

namespace Zahy.Settlement;

public class VatMathAndConfigTests
{
    [Fact]
    public void Back_Out_Is_Round_Per_Line_And_Preserves_Inclusive()
    {
        // 15 inclusive @ 15% → net 13.04, vat 1.96, and net + vat == 15.00 exactly.
        VatMath.NetOfInclusive(15m, 0.15m).ShouldBe(13.04m);
        VatMath.VatOfInclusive(15m, 0.15m).ShouldBe(1.96m);
        (VatMath.NetOfInclusive(15m, 0.15m) + VatMath.VatOfInclusive(15m, 0.15m)).ShouldBe(15.00m);

        // 10 inclusive: back-out gives 1.30 (NOT net×rate = 1.31, which would break the inclusive total).
        VatMath.VatOfInclusive(10m, 0.15m).ShouldBe(1.30m);
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(1.0)]
    public void Invalid_Vat_Rate_Is_Rejected(decimal rate)
    {
        Should.Throw<BusinessException>(() => VatMath.NetOfInclusive(100m, rate))
            .Code.ShouldBe(SettlementVatErrorCodes.InvalidVatRate);
    }

    [Fact]
    public void Cost_Markup_Line_Requires_Vat_Inclusive_Prices_And_Matching_Currency()
    {
        Should.Throw<BusinessException>(() =>
                CostMarkupLine.Of(Money.Of(10m, vatInclusive: false), Money.Of(15m, vatInclusive: true)))
            .Code.ShouldBe(SettlementVatErrorCodes.PriceMustBeVatInclusive);

        Should.Throw<BusinessException>(() =>
                CostMarkupLine.Of(Money.Of(10m, "SAR", vatInclusive: true), Money.Of(15m, "USD", vatInclusive: true)))
            .Code.ShouldBe(SettlementVatErrorCodes.PriceCurrencyMismatch);
    }

    [Fact]
    public void Treatment_Is_Config_Not_Hardcoded()
    {
        var options = new SettlementVatOptions
        {
            StandardRate = 0.15m,
            TreatmentByBook = { [SettlementBook.Integration] = VatTreatment.Principal }
        };

        options.ResolveTreatment(SettlementBook.Integration).ShouldBe(VatTreatment.Principal);

        // An unconfigured book throws rather than silently defaulting — forces an accountant decision.
        Should.Throw<BusinessException>(() => options.ResolveTreatment(SettlementBook.Marketplace))
            .Code.ShouldBe(SettlementVatErrorCodes.TreatmentNotConfigured);
    }
}
