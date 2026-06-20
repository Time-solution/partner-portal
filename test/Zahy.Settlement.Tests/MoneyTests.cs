using Shouldly;
using Volo.Abp;
using Xunit;

namespace Zahy.Settlement;

public class MoneyTests
{
    [Fact]
    public void Of_Rounds_To_Two_Decimals_Away_From_Zero()
    {
        Money.Of(8.695m).Amount.ShouldBe(8.70m);
        Money.Of(13.043478m).Amount.ShouldBe(13.04m);
        Money.Of(1.955m).Amount.ShouldBe(1.96m);
        Money.Of(-1.955m).Amount.ShouldBe(-1.96m);
        Money.Of(8.694m).Amount.ShouldBe(8.69m);
    }

    [Fact]
    public void Of_Defaults_To_Sar_And_Uppercases_Currency()
    {
        Money.Of(10m).Currency.ShouldBe("SAR");
        Money.Of(10m, "usd").Currency.ShouldBe("USD");
    }

    [Theory]
    [InlineData("")]
    [InlineData("SR")]
    [InlineData("SARS")]
    [InlineData("S1R")]
    public void Of_Rejects_Invalid_Currency(string currency)
    {
        Should.Throw<BusinessException>(() => Money.Of(10m, currency))
            .Code.ShouldBe(SettlementErrorCodes.InvalidCurrency);
    }

    [Fact]
    public void Add_And_Subtract_Same_Currency()
    {
        (Money.Of(10m) + Money.Of(5.05m)).Amount.ShouldBe(15.05m);
        (Money.Of(10m) - Money.Of(5.05m)).Amount.ShouldBe(4.95m);
    }

    [Fact]
    public void Arithmetic_Rejects_Currency_Mismatch()
    {
        Should.Throw<BusinessException>(() => Money.Of(10m, "SAR") + Money.Of(10m, "USD"))
            .Code.ShouldBe(SettlementErrorCodes.CurrencyMismatch);
    }

    [Fact]
    public void Arithmetic_Rejects_VatInclusive_Mismatch()
    {
        Should.Throw<BusinessException>(() =>
                Money.Of(10m, vatInclusive: true) + Money.Of(10m, vatInclusive: false))
            .Code.ShouldBe(SettlementErrorCodes.VatInclusiveMismatch);
    }

    [Fact]
    public void Sign_Helpers_And_Negation()
    {
        Money.Of(5m).IsPositive.ShouldBeTrue();
        Money.Of(-5m).IsNegative.ShouldBeTrue();
        Money.Zero().IsZero.ShouldBeTrue();
        (-Money.Of(5m)).Amount.ShouldBe(-5m);
        Money.Of(-5m).Abs().Amount.ShouldBe(5m);
    }

    [Fact]
    public void Value_Equality_Considers_Amount_Currency_And_VatFlag()
    {
        Money.Of(10m).ShouldBe(Money.Of(10m));
        Money.Of(10m).ShouldNotBe(Money.Of(10m, "USD"));
        Money.Of(10m, vatInclusive: true).ShouldNotBe(Money.Of(10m, vatInclusive: false));
    }
}
