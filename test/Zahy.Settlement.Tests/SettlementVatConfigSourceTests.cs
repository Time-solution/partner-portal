using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Shouldly;
using Xunit;

namespace Zahy.Settlement;

/// <summary>
/// F6 — the VAT rate is CONFIG, never a second hardcoded literal. The default everywhere now resolves
/// to <see cref="SettlementVatOptions.DefaultStandardRate"/> (the one place 0.15 lives), and the compute
/// paths honour whatever rate they are handed rather than a baked-in 15%.
/// </summary>
public class SettlementVatConfigSourceTests
{
    private static Money Incl(decimal amount) =>
        Money.Of(amount, SettlementConsts.DefaultCurrency, vatInclusive: true);

    private static decimal OutputVatOf(PostingResult r) =>
        r.LineFor(SettlementAccountCode.OutputVat, EntryDirection.Credit).ShouldNotBeNull().Amount.Amount;

    private sealed class FakeCaseStore : ISettlementCaseStore
    {
        public readonly List<SettlementCase> Cases = new();

        public Task<SettlementCase?> FindByKeyAsync(SettlementBook book, string ext, CancellationToken ct = default) =>
            Task.FromResult(Cases.FirstOrDefault(c => c.Book == book && c.ExternalTransactionId == ext));

        public Task InsertAsync(SettlementCase c, CancellationToken ct = default)
        {
            Cases.Add(c);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(SettlementCase c, CancellationToken ct = default) => Task.CompletedTask;
    }

    private static SettlementResaleTriggerService BuildTrigger() =>
        new(
            new ResaleVatCalculator(),
            new FakeCaseStore(),
            new SettlementFlowProfileResolver(new ISettlementFlowProfile[]
            {
                new AggregatorFlowProfile(),
                new ServiceFlowProfile()
            }));

    [Fact]
    public void ActivationFeeComputer_Uses_Config_VatRate_Not_Literal()
    {
        // The compute-only default is tied to the config source, not an independent 0.15 literal.
        ActivationFeeComputer.DefaultVatRate.ShouldBe(new SettlementVatOptions().StandardRate);

        var config = new ActivationFeeConfig(
            Guid.NewGuid(),
            Guid.NewGuid(),
            ActivationFeeLine.Of(true, Incl(40.00m), ActivationFeePayer.Merchant),
            ActivationFeeLine.Off());

        // Standard configured rate (0.15): 40 incl → 5.22 output VAT (the anchored mock number).
        var atStandard = ActivationFeeComputer.Subscription(config, new SettlementVatOptions().StandardRate)
            .ShouldNotBeNull();
        OutputVatOf(atStandard).ShouldBe(5.22m);

        // A DIFFERENT configured rate changes the computed fee — proving the rate flows through, not a literal.
        var atTen = ActivationFeeComputer.Subscription(config, vatRate: 0.10m).ShouldNotBeNull();
        OutputVatOf(atTen).ShouldNotBe(OutputVatOf(atStandard));
        OutputVatOf(atTen).ShouldBe(3.64m); // 40 − 40/1.10
    }

    [Fact]
    public async Task SettlementResaleTrigger_Uses_Config_VatRate_Not_Literal()
    {
        // The request default is sourced from the config class, not a re-typed 0.15.
        new SettlementResaleTriggerRequest().VatRate.ShouldBe(new SettlementVatOptions().StandardRate);

        // Standard configured rate: sell 13 incl → 1.70 output VAT.
        var atStandard = await BuildTrigger().TriggerPrincipalResaleAsync(new SettlementResaleTriggerRequest
        {
            Book = SettlementBook.Integration,
            PartnerId = Guid.NewGuid(),
            ExternalTransactionId = "order:vat-cfg:v1",
            BuyPrice = Incl(10.00m),
            SellPrice = Incl(13.00m),
            VatRate = new SettlementVatOptions().StandardRate,
            PostedAt = DateTime.UtcNow
        });
        atStandard.OutputVat.ShouldBe(1.70m);

        // A different rate produces a different VAT — the computation honours the (configurable) rate.
        var atTen = await BuildTrigger().TriggerPrincipalResaleAsync(new SettlementResaleTriggerRequest
        {
            Book = SettlementBook.Integration,
            PartnerId = Guid.NewGuid(),
            ExternalTransactionId = "order:vat-cfg-10:v1",
            BuyPrice = Incl(10.00m),
            SellPrice = Incl(13.00m),
            VatRate = 0.10m,
            PostedAt = DateTime.UtcNow
        });
        atTen.OutputVat.ShouldNotBe(atStandard.OutputVat);
    }
}
