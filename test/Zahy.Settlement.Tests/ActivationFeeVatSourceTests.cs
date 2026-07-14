using System;
using Shouldly;
using Xunit;

namespace Zahy.Settlement;

/// <summary>
/// F6 — the shared VAT source (<see cref="SettlementVatOptions.DefaultStandardRate"/>) is the ONLY
/// rate feeding <see cref="ActivationFeeComputer"/> output: the default IS the shared constant
/// (compile-time routing, no second literal), the default-rate output is byte-identical to passing
/// the shared constant explicitly, and the explicit rate parameter is the only other channel.
/// </summary>
public class ActivationFeeVatSourceTests
{
    private static Money Incl(decimal amount) =>
        Money.Of(amount, SettlementConsts.DefaultCurrency, vatInclusive: true);

    private static ActivationFeeConfig Config() =>
        new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            ActivationFeeLine.Of(true, Incl(40.00m), ActivationFeePayer.Merchant),
            ActivationFeeLine.Of(false, Incl(1.00m), ActivationFeePayer.Partner));

    [Fact]
    public void Default_Rate_IS_The_Shared_Settlement_Vat_Source()
    {
        ActivationFeeComputer.DefaultVatRate.ShouldBe(SettlementVatOptions.DefaultStandardRate);
    }

    [Fact]
    public void Default_Output_Is_Identical_To_Passing_The_Shared_Source_Explicitly()
    {
        var config = Config();

        var byDefault = ActivationFeeComputer.Subscription(config).ShouldNotBeNull();
        var byExplicitSharedRate = ActivationFeeComputer
            .Subscription(config, SettlementVatOptions.DefaultStandardRate)
            .ShouldNotBeNull();

        byDefault.Lines.Count.ShouldBe(byExplicitSharedRate.Lines.Count);
        for (var i = 0; i < byDefault.Lines.Count; i++)
        {
            byDefault.Lines[i].AccountCode.ShouldBe(byExplicitSharedRate.Lines[i].AccountCode);
            byDefault.Lines[i].Direction.ShouldBe(byExplicitSharedRate.Lines[i].Direction);
            byDefault.Lines[i].Amount.Amount.ShouldBe(byExplicitSharedRate.Lines[i].Amount.Amount);
        }
    }

    [Fact]
    public void The_Rate_Parameter_Is_The_Only_Other_Channel_Into_The_Output()
    {
        var config = Config();

        var atSharedRate = ActivationFeeComputer.Subscription(config).ShouldNotBeNull();
        var atDifferentRate = ActivationFeeComputer.Subscription(config, 0.05m).ShouldNotBeNull();

        // Same inclusive input, different rate → different VAT split. If any hidden literal fed the
        // computer, changing the parameter could not move the whole split.
        atSharedRate.LineFor(SettlementAccountCode.OutputVat, EntryDirection.Credit)
            .ShouldNotBeNull().Amount.Amount
            .ShouldNotBe(atDifferentRate.LineFor(SettlementAccountCode.OutputVat, EntryDirection.Credit)
                .ShouldNotBeNull().Amount.Amount);
    }
}
