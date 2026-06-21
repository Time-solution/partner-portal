using System;
using Shouldly;
using Xunit;

namespace Zahy.Settlement;

/// <summary>
/// Phase D Task 4 — ReflectionLog display fields carry NO financial impact, and the trial balance
/// still nets to zero across every flow (principal + payer-selectable fees + reflection-only).
/// </summary>
public class ReflectionDisplayAndTrialBalanceTests
{
    private const decimal Vat = 0.15m;
    private static readonly SettlementPeriod Period = SettlementPeriod.Of(2026, 6);
    private static readonly Guid Partner = Guid.NewGuid();
    private static readonly Guid Merchant = Guid.NewGuid();

    private static Money Incl(decimal amount) => Money.Of(amount, SettlementConsts.DefaultCurrency, vatInclusive: true);

    [Fact]
    public void ReflectionLog_With_Display_Detail_Stores_Fields_Without_Financial_Impact()
    {
        var log = new ReflectionLog(Guid.NewGuid(), "HS-1", Incl(23.00m), Merchant, DateTime.UtcNow)
            .WithDisplayDetail(menuPrice: 10.00m, partnerListPrice: 13.00m, deliveryFee: 10.00m, customerPaid: 23.00m);

        log.MenuPrice.ShouldBe(10.00m);
        log.PartnerListPrice.ShouldBe(13.00m);
        log.DeliveryFee.ShouldBe(10.00m);
        log.CustomerPaid.ShouldBe(23.00m);

        // The matching ReflectionOnly posting result carries NO financial lines.
        var reflection = SettlementPostingTemplates.ReflectionOnly();
        reflection.IsFinancial.ShouldBeFalse();
        reflection.Lines.Count.ShouldBe(0);
    }

    [Fact]
    public void ReflectionOnly_Contributes_Zero_To_Margin_Vat_And_Trial_Balance()
    {
        var principal = SettlementPostingTemplates.Principal(Incl(100.00m), Incl(70.00m), Vat)
            .Tag(Partner, Merchant, Period, "ORD-1");
        var feeMerchant = SettlementPostingTemplates.Fee(Incl(40.00m), ActivationFeePayer.Merchant, Vat)
            .Tag(Partner, Merchant, Period, "FEE-SUB");
        var feePartner = SettlementPostingTemplates.Fee(Incl(1.00m), ActivationFeePayer.Partner, Vat)
            .Tag(Partner, Merchant, Period, "FEE-TXN");
        var reflection = SettlementPostingTemplates.ReflectionOnly()
            .Tag(Partner, Merchant, Period, "HS-1");

        var withReflection = new[] { principal, feeMerchant, feePartner, reflection };
        var withoutReflection = new[] { principal, feeMerchant, feePartner };

        var platformWith = SettlementReports.Platform(withReflection, Period);
        var platformWithout = SettlementReports.Platform(withoutReflection, Period);

        // Adding the reflection-only entry changes NO financial figure — only the reflection count.
        platformWith.ResaleMargin.Amount.ShouldBe(platformWithout.ResaleMargin.Amount);
        platformWith.FeeRevenue.Amount.ShouldBe(platformWithout.FeeRevenue.Amount);
        platformWith.NetVatToZatca.Amount.ShouldBe(platformWithout.NetVatToZatca.Amount);
        platformWith.ReflectionCount.ShouldBe(1);

        // Fee revenue rolls up both fee lines (34.78 + 0.87).
        platformWith.FeeRevenue.Amount.ShouldBe(35.65m);

        // Trial balance nets to zero across all flows.
        var trial = SettlementReports.TrialBalanceFor(withReflection, Period);
        trial.IsBalanced.ShouldBeTrue();
        trial.Net.ShouldBe(0m);
    }
}
