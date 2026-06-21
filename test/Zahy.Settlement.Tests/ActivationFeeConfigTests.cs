using System;
using Shouldly;
using Xunit;

namespace Zahy.Settlement;

/// <summary>
/// Phase D Task 1 — ActivationFeeConfig is CONFIG ONLY. Two independently toggleable lines, both may
/// be ON and bill different sides. Holding the config posts no journal (mirrors the frontend mock shape).
/// </summary>
public class ActivationFeeConfigTests
{
    private static Money Incl(decimal amount) => Money.Of(amount, SettlementConsts.DefaultCurrency, vatInclusive: true);

    [Fact]
    public void Holds_Both_Lines_Independently_With_Different_Payers()
    {
        var config = new ActivationFeeConfig(
            Guid.NewGuid(),
            Guid.NewGuid(),
            ActivationFeeLine.Of(true, Incl(40.00m), ActivationFeePayer.Merchant),
            ActivationFeeLine.Of(true, Incl(1.00m), ActivationFeePayer.Partner));

        config.Subscription.Enabled.ShouldBeTrue();
        config.Subscription.AmountInclusive.Amount.ShouldBe(40.00m);
        config.Subscription.AmountInclusive.VatInclusive.ShouldBeTrue();
        config.Subscription.Payer.ShouldBe(ActivationFeePayer.Merchant);

        config.PerTransaction.Enabled.ShouldBeTrue();
        config.PerTransaction.AmountInclusive.Amount.ShouldBe(1.00m);
        config.PerTransaction.Payer.ShouldBe(ActivationFeePayer.Partner);
    }

    [Fact]
    public void Lines_Can_Be_Toggled_Independently()
    {
        var config = new ActivationFeeConfig(
            Guid.NewGuid(),
            Guid.NewGuid(),
            ActivationFeeLine.Of(true, Incl(40.00m), ActivationFeePayer.Merchant),
            ActivationFeeLine.Off());

        config.Subscription.Enabled.ShouldBeTrue();
        config.PerTransaction.Enabled.ShouldBeFalse();

        config.SetSubscription(ActivationFeeLine.Off());
        config.SetPerTransaction(ActivationFeeLine.Of(true, Incl(1.00m), ActivationFeePayer.Merchant));

        config.Subscription.Enabled.ShouldBeFalse();
        config.PerTransaction.Enabled.ShouldBeTrue();
        config.PerTransaction.AmountInclusive.Amount.ShouldBe(1.00m);
    }

    [Fact]
    public void Fee_Line_Rejects_A_Non_VatInclusive_Amount()
    {
        Should.Throw<Volo.Abp.BusinessException>(() =>
            ActivationFeeLine.Of(true, Money.Of(40.00m, "SAR", vatInclusive: false), ActivationFeePayer.Merchant));
    }
}
