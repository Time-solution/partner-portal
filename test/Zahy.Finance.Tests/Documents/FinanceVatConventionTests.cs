using System.Globalization;
using Shouldly;
using Xunit;
using SettlementVatMath = Zahy.Settlement.VatMath;

namespace Zahy.Finance.Documents;

/// <summary>
/// Pins the VAT convention fix: Zahy.Finance treats invoice amounts as VAT-INCLUSIVE (the SAME convention as
/// the Settlement engine / frontend splitInclusiveVat), so it BACKS OUT net + VAT from the inclusive amount
/// rather than ADDING 15% on top. Pure unit tests — no DB / ABP host needed.
/// </summary>
public class FinanceVatConventionTests
{
    private const decimal Rate = 0.15m;

    [Fact]
    public void Inclusive_100_backs_out_to_86_96_plus_13_04_not_100_plus_15()
    {
        const decimal inclusive = 100m;

        var net = FinanceVat.NetOfInclusive(inclusive, Rate);
        var vat = FinanceVat.VatOfInclusive(inclusive, Rate);

        net.ShouldBe(86.96m);
        vat.ShouldBe(13.04m);

        // The inclusive amount is conserved: net + VAT == 100.00 (NOT 100 + 15 = 115).
        (net + vat).ShouldBe(100.00m);
        (net + vat).ShouldNotBe(115.00m);
        vat.ShouldNotBe(15.00m);
    }

    [Theory]
    [InlineData("100", "86.96", "13.04")]
    [InlineData("25", "21.74", "3.26")]
    [InlineData("15", "13.04", "1.96")]
    [InlineData("149", "129.57", "19.43")]
    public void Known_inclusive_cases_split_correctly_and_conserve_total(
        string inclusiveText,
        string expectedNetText,
        string expectedVatText)
    {
        var inclusive = decimal.Parse(inclusiveText, CultureInfo.InvariantCulture);
        var expectedNet = decimal.Parse(expectedNetText, CultureInfo.InvariantCulture);
        var expectedVat = decimal.Parse(expectedVatText, CultureInfo.InvariantCulture);

        var net = FinanceVat.NetOfInclusive(inclusive, Rate);
        var vat = FinanceVat.VatOfInclusive(inclusive, Rate);

        net.ShouldBe(expectedNet);
        vat.ShouldBe(expectedVat);
        (net + vat).ShouldBe(inclusive);
    }

    [Theory]
    [InlineData("100")]
    [InlineData("25")]
    [InlineData("15")]
    [InlineData("30")]
    [InlineData("149")]
    [InlineData("0.05")]
    [InlineData("7")]
    [InlineData("11")]
    [InlineData("12345.67")]
    public void Finance_and_Settlement_split_an_identical_inclusive_input_identically(string inclusiveText)
    {
        var inclusive = decimal.Parse(inclusiveText, CultureInfo.InvariantCulture);

        // Same money in -> same net and same VAT out, across both modules' VAT helpers.
        FinanceVat.NetOfInclusive(inclusive, Rate).ShouldBe(SettlementVatMath.NetOfInclusive(inclusive, Rate));
        FinanceVat.VatOfInclusive(inclusive, Rate).ShouldBe(SettlementVatMath.VatOfInclusive(inclusive, Rate));
    }
}
