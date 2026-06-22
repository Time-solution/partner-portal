using System;
using Shouldly;
using Volo.Abp;
using Xunit;
using Zahy.PartnerCatalog.Packages;
using Zahy.Settlement;

namespace Zahy.PartnerCatalog;

public class UsagePackageTests
{
    private static readonly Guid PartnerId = Guid.Parse("22222222-2222-2222-2222-222222222004");

    private static UsagePackage Resale() => new(
        Guid.NewGuid(), PartnerId, "Resale", "messages", UsagePackageMode.Resale,
        "SAR", includedQuantity: 1000m, baseBuyAmount: 80m, baseSellAmount: 120m,
        overageBuyAmount: 0.01m, overageSellAmount: 0.04m, payer: ActivationFeePayer.Merchant);

    private static UsagePackage Subscription() => new(
        Guid.NewGuid(), PartnerId, "Subscription", "messages", UsagePackageMode.Subscription,
        "SAR", includedQuantity: 5000m, baseBuyAmount: 999m, baseSellAmount: 149m,
        overageBuyAmount: 999m, overageSellAmount: 0.03m, payer: ActivationFeePayer.Partner);

    [Fact]
    public void Subscription_Mode_Zeroes_Buy_Side()
    {
        var pkg = Subscription();
        pkg.BaseBuyAmount.ShouldBe(0m);
        pkg.OverageBuyAmount.ShouldBe(0m);
        pkg.BaseSellAmount.ShouldBe(149m);
        pkg.Payer.ShouldBe(ActivationFeePayer.Partner);
    }

    [Fact]
    public void Pure_Per_Use_Is_Zero_Base_Zero_Included()
    {
        var pkg = new UsagePackage(
            Guid.NewGuid(), PartnerId, "Pure per-use", "messages", UsagePackageMode.Resale,
            "SAR", includedQuantity: 0m, baseBuyAmount: 0m, baseSellAmount: 0m,
            overageBuyAmount: 0.02m, overageSellAmount: 0.05m, payer: ActivationFeePayer.Merchant);

        pkg.IncludedQuantity.ShouldBe(0m);
        pkg.BaseBuyAmount.ShouldBe(0m);
        pkg.OverageSellAmount.ShouldBe(0.05m);
    }

    [Fact]
    public void Negative_Amount_Is_Rejected()
    {
        var ex = Should.Throw<BusinessException>(() => new UsagePackage(
            Guid.NewGuid(), PartnerId, "Bad", "messages", UsagePackageMode.Resale,
            "SAR", includedQuantity: 0m, baseBuyAmount: -1m, baseSellAmount: 0m,
            overageBuyAmount: 0m, overageSellAmount: 0m, payer: ActivationFeePayer.Merchant));
        ex.Code.ShouldBe(PartnerCatalogErrorCodes.InvalidUsagePackage);
    }

    [Fact]
    public void Archived_Cannot_Be_Published()
    {
        var pkg = Resale();
        pkg.Archive();
        var ex = Should.Throw<BusinessException>(() => pkg.Publish());
        ex.Code.ShouldBe(PartnerCatalogErrorCodes.InvalidUsagePackageStatusTransition);
    }

    [Fact]
    public void Admin_View_Has_Margin()
    {
        var dto = UsagePackageVisibility.ToDto(Resale(), UsagePackageAudience.Admin);
        dto.BaseBuy!.Amount.ShouldBe(80m);
        dto.BaseSell!.Amount.ShouldBe(120m);
        dto.BaseMargin!.Amount.ShouldBe(40m);
        dto.OverageMargin!.Amount.ShouldBe(0.03m);
    }

    [Fact]
    public void Partner_Resale_View_Omits_Sell_And_Margin()
    {
        var dto = UsagePackageVisibility.ToDto(Resale(), UsagePackageAudience.Partner);
        dto.BaseBuy.ShouldNotBeNull();
        dto.BaseSell.ShouldBeNull();
        dto.OverageSell.ShouldBeNull();
        dto.BaseMargin.ShouldBeNull();
        dto.OverageMargin.ShouldBeNull();
    }

    [Fact]
    public void Merchant_Resale_View_Omits_Buy_And_Margin()
    {
        var dto = UsagePackageVisibility.ToDto(Resale(), UsagePackageAudience.Merchant);
        dto.BaseSell.ShouldNotBeNull();
        dto.BaseBuy.ShouldBeNull();
        dto.OverageBuy.ShouldBeNull();
        dto.BaseMargin.ShouldBeNull();
        dto.OverageMargin.ShouldBeNull();
    }

    [Fact]
    public void Subscription_View_Shows_Fee_And_Payer_No_Buy_Margin()
    {
        var dto = UsagePackageVisibility.ToDto(Subscription(), UsagePackageAudience.Partner);
        dto.BaseSell!.Amount.ShouldBe(149m);
        dto.Payer.ShouldBe(ActivationFeePayer.Partner);
        dto.BaseBuy.ShouldBeNull();
        dto.BaseMargin.ShouldBeNull();
    }
}
