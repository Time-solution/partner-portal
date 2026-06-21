using System.Linq;
using Shouldly;
using Xunit;

namespace Zahy.Settlement;

/// <summary>
/// Phase D Task 2 — payer-selectable fee posting templates (compute only, no journal posted).
/// Anchored to the mock numbers: 1.00 incl → 0.87 + 0.13; 40.00 incl → 34.78 + 5.22.
/// </summary>
public class ActivationFeeTemplatesTests
{
    private const decimal Vat = 0.15m;

    private static Money Incl(decimal amount) => Money.Of(amount, SettlementConsts.DefaultCurrency, vatInclusive: true);

    private static decimal Amount(PostingResult r, string code, EntryDirection dir) =>
        r.LineFor(code, dir).ShouldNotBeNull().Amount.Amount;

    [Fact]
    public void PerTransaction_Fee_1_Incl_Splits_To_087_Ex_And_013_Vat()
    {
        var result = SettlementPostingTemplates.Fee(Incl(1.00m), ActivationFeePayer.Merchant, Vat);

        Amount(result, SettlementAccountCode.FeeRevenue, EntryDirection.Credit).ShouldBe(0.87m);
        Amount(result, SettlementAccountCode.OutputVat, EntryDirection.Credit).ShouldBe(0.13m);
        result.IsBalanced.ShouldBeTrue();
        result.TrialBalanceNet.ShouldBe(0m);
    }

    [Fact]
    public void Subscription_Fee_40_Incl_Splits_To_3478_Ex_And_522_Vat()
    {
        var result = SettlementPostingTemplates.Fee(Incl(40.00m), ActivationFeePayer.Merchant, Vat);

        Amount(result, SettlementAccountCode.FeeRevenue, EntryDirection.Credit).ShouldBe(34.78m);
        Amount(result, SettlementAccountCode.OutputVat, EntryDirection.Credit).ShouldBe(5.22m);
        result.IsBalanced.ShouldBeTrue();
    }

    [Fact]
    public void Payer_Merchant_Debits_1200_Ar_Merchant()
    {
        var result = SettlementPostingTemplates.Fee(Incl(40.00m), ActivationFeePayer.Merchant, Vat);

        Amount(result, SettlementAccountCode.ArMerchant, EntryDirection.Debit).ShouldBe(40.00m);
        result.LineFor(SettlementAccountCode.ArPartner, EntryDirection.Debit).ShouldBeNull();
    }

    [Fact]
    public void Payer_Partner_Debits_1250_Ar_Partner()
    {
        var result = SettlementPostingTemplates.Fee(Incl(40.00m), ActivationFeePayer.Partner, Vat);

        Amount(result, SettlementAccountCode.ArPartner, EntryDirection.Debit).ShouldBe(40.00m);
        result.LineFor(SettlementAccountCode.ArMerchant, EntryDirection.Debit).ShouldBeNull();
    }

    [Fact]
    public void Fee_Template_Has_No_Cost_Leg_No_1300_No_5100()
    {
        var result = SettlementPostingTemplates.Fee(Incl(40.00m), ActivationFeePayer.Partner, Vat);

        result.LineFor(SettlementAccountCode.InputVat, EntryDirection.Debit).ShouldBeNull();
        result.LineFor(SettlementAccountCode.PartnerCogs, EntryDirection.Debit).ShouldBeNull();
        result.Lines.Count.ShouldBe(3);
    }

    [Fact]
    public void Both_Fees_On_Yields_Two_Independent_Computations()
    {
        // Subscription 40 incl billed to the merchant; per-txn 1 incl billed to the partner.
        var config = new ActivationFeeConfig(
            System.Guid.NewGuid(),
            System.Guid.NewGuid(),
            ActivationFeeLine.Of(true, Incl(40.00m), ActivationFeePayer.Merchant),
            ActivationFeeLine.Of(true, Incl(1.00m), ActivationFeePayer.Partner));

        // 3 successful transactions → 1 subscription result + 3 per-transaction results.
        var results = ActivationFeeComputer.ComputeForPeriod(config, successfulTransactionCount: 3, Vat);
        results.Count.ShouldBe(4);

        var subscription = ActivationFeeComputer.Subscription(config, Vat).ShouldNotBeNull();
        Amount(subscription, SettlementAccountCode.ArMerchant, EntryDirection.Debit).ShouldBe(40.00m);

        var perTxn = ActivationFeeComputer.PerTransaction(config, 3, Vat);
        perTxn.Count.ShouldBe(3);
        perTxn.ShouldAllBe(r => r.LineFor(SettlementAccountCode.ArPartner, EntryDirection.Debit) != null);
    }

    [Fact]
    public void Disabled_Lines_Compute_Nothing()
    {
        var config = new ActivationFeeConfig(
            System.Guid.NewGuid(),
            System.Guid.NewGuid(),
            ActivationFeeLine.Off(),
            ActivationFeeLine.Off());

        ActivationFeeComputer.ComputeForPeriod(config, successfulTransactionCount: 5, Vat).ShouldBeEmpty();
        ActivationFeeComputer.Subscription(config, Vat).ShouldBeNull();
        ActivationFeeComputer.PerTransaction(config, 5, Vat).ShouldBeEmpty();
    }
}
