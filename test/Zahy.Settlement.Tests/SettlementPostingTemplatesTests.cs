using Shouldly;
using Xunit;

namespace Zahy.Settlement;

public class SettlementPostingTemplatesTests
{
    private const decimal Vat = 0.15m;

    private static Money Incl(decimal amount) => Money.Of(amount, SettlementConsts.DefaultCurrency, vatInclusive: true);

    private static decimal Amount(PostingResult r, string code, EntryDirection dir) =>
        r.LineFor(code, dir).ShouldNotBeNull().Amount.Amount;

    [Fact]
    public void Principal_70_to_100_Posts_Exact_Lines_And_Balances()
    {
        var result = SettlementPostingTemplates.Principal(Incl(100.00m), Incl(70.00m), Vat);

        result.Mode.ShouldBe(ParticipationMode.Principal);
        result.Lines.Count.ShouldBe(6);

        Amount(result, SettlementAccountCode.ArMerchant, EntryDirection.Debit).ShouldBe(100.00m);
        Amount(result, SettlementAccountCode.ResaleRevenue, EntryDirection.Credit).ShouldBe(86.96m);
        Amount(result, SettlementAccountCode.OutputVat, EntryDirection.Credit).ShouldBe(13.04m);
        Amount(result, SettlementAccountCode.PartnerCogs, EntryDirection.Debit).ShouldBe(60.87m);
        Amount(result, SettlementAccountCode.InputVat, EntryDirection.Debit).ShouldBe(9.13m);
        Amount(result, SettlementAccountCode.ApPartner, EntryDirection.Credit).ShouldBe(70.00m);

        result.NetVat.Amount.ShouldBe(3.91m);
        result.Margin.Amount.ShouldBe(26.09m);

        result.IsBalanced.ShouldBeTrue();
        result.TotalDebits.Amount.ShouldBe(170.00m);
        result.TotalCredits.Amount.ShouldBe(170.00m);
        result.TrialBalanceNet.ShouldBe(0m);
    }

    [Fact]
    public void Principal_10_to_13_Posts_Exact_Lines_And_Balances()
    {
        var result = SettlementPostingTemplates.Principal(Incl(13.00m), Incl(10.00m), Vat);

        Amount(result, SettlementAccountCode.ArMerchant, EntryDirection.Debit).ShouldBe(13.00m);
        Amount(result, SettlementAccountCode.ResaleRevenue, EntryDirection.Credit).ShouldBe(11.30m);
        Amount(result, SettlementAccountCode.OutputVat, EntryDirection.Credit).ShouldBe(1.70m);
        Amount(result, SettlementAccountCode.PartnerCogs, EntryDirection.Debit).ShouldBe(8.70m);
        Amount(result, SettlementAccountCode.InputVat, EntryDirection.Debit).ShouldBe(1.30m);
        Amount(result, SettlementAccountCode.ApPartner, EntryDirection.Credit).ShouldBe(10.00m);

        result.NetVat.Amount.ShouldBe(0.40m);
        result.Margin.Amount.ShouldBe(2.60m);

        result.IsBalanced.ShouldBeTrue();
        result.TrialBalanceNet.ShouldBe(0m);
    }

    [Fact]
    public void SubscriptionFee_100_Incl_Posts_Three_Lines_No_Cost_Leg()
    {
        var result = SettlementPostingTemplates.SubscriptionFee(Incl(100.00m), Vat);

        result.Mode.ShouldBe(ParticipationMode.SubscriptionFee);
        result.Lines.Count.ShouldBe(3);

        Amount(result, SettlementAccountCode.ArMerchant, EntryDirection.Debit).ShouldBe(100.00m);
        Amount(result, SettlementAccountCode.FeeRevenue, EntryDirection.Credit).ShouldBe(86.96m);
        Amount(result, SettlementAccountCode.OutputVat, EntryDirection.Credit).ShouldBe(13.04m);

        // No cost leg.
        result.LineFor(SettlementAccountCode.InputVat, EntryDirection.Debit).ShouldBeNull();
        result.LineFor(SettlementAccountCode.PartnerCogs, EntryDirection.Debit).ShouldBeNull();

        result.NetVat.Amount.ShouldBe(13.04m);
        result.IsBalanced.ShouldBeTrue();
        result.TrialBalanceNet.ShouldBe(0m);
    }

    [Fact]
    public void ReflectionOnly_Produces_No_Financial_Lines_And_Balances_Trivially()
    {
        var result = SettlementPostingTemplates.ReflectionOnly();

        result.Mode.ShouldBe(ParticipationMode.ReflectionOnly);
        result.IsFinancial.ShouldBeFalse();
        result.Lines.Count.ShouldBe(0);
        result.IsBalanced.ShouldBeTrue();
        result.TrialBalanceNet.ShouldBe(0m);
    }
}
