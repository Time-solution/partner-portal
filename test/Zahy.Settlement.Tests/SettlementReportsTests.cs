using System;
using System.Linq;
using Shouldly;
using Xunit;

namespace Zahy.Settlement;

public class SettlementReportsTests
{
    private const decimal Vat = 0.15m;

    private static readonly SettlementPeriod Period = SettlementPeriod.Of(2026, 6);
    private static readonly SettlementPeriod OtherPeriod = SettlementPeriod.Of(2026, 5);

    private static readonly Guid PartnerA = Guid.NewGuid();
    private static readonly Guid PartnerB = Guid.NewGuid();
    private static readonly Guid MerchantA = Guid.NewGuid();
    private static readonly Guid MerchantB = Guid.NewGuid();
    private static readonly Guid MerchantC = Guid.NewGuid();

    private static Money Incl(decimal amount) => Money.Of(amount, SettlementConsts.DefaultCurrency, vatInclusive: true);

    // Scenario S = {Principal 70->100, Principal 10->13, SubscriptionFee 100} (+ one ReflectionOnly).
    private static PostingResult O1 => SettlementPostingTemplates.Principal(Incl(100m), Incl(70m), Vat)
        .Tag(PartnerA, MerchantA, Period, "ord-1");

    private static PostingResult O2 => SettlementPostingTemplates.Principal(Incl(13m), Incl(10m), Vat)
        .Tag(PartnerB, MerchantB, Period, "ord-2");

    private static PostingResult O3 => SettlementPostingTemplates.SubscriptionFee(Incl(100m), Vat)
        .Tag(Guid.Empty, MerchantC, Period, "ord-3");

    private static PostingResult Reflection => SettlementPostingTemplates.ReflectionOnly()
        .Tag(Guid.Empty, MerchantA, Period, "ord-ref");

    private static PostingResult[] ScenarioS() => new[] { O1, O2, O3, Reflection };

    [Fact]
    public void Order_Report_Carries_Lines_Margin_And_NetVat()
    {
        var report = SettlementReports.Order(O1);

        report.OrderRef.ShouldBe("ord-1");
        report.Mode.ShouldBe(ParticipationMode.Principal);
        report.Lines.Count.ShouldBe(6);
        report.Margin.Amount.ShouldBe(26.09m);
        report.NetVat.Amount.ShouldBe(3.91m);
        report.IsBalanced.ShouldBeTrue();
        report.TrialBalanceNet.ShouldBe(0m);
    }

    [Fact]
    public void Trial_Balance_Nets_To_Zero()
    {
        var tb = SettlementReports.TrialBalanceFor(ScenarioS(), Period);

        tb.IsBalanced.ShouldBeTrue();
        tb.TotalDebits.Amount.ShouldBe(293.00m);
        tb.TotalCredits.Amount.ShouldBe(293.00m);
        tb.Net.ShouldBe(0m);
    }

    [Fact]
    public void Vat_Control_Is_NetOutputMinusInput()
    {
        var vat = SettlementReports.VatControl(ScenarioS(), Period);

        vat.OutputVat.Amount.ShouldBe(27.78m);
        vat.InputVat.Amount.ShouldBe(10.43m);
        vat.NetVatPayable.Amount.ShouldBe(17.35m);
    }

    [Fact]
    public void Platform_Report_Aggregates_Margin_Fee_And_NetVat()
    {
        var platform = SettlementReports.Platform(ScenarioS(), Period);

        platform.ResaleMargin.Amount.ShouldBe(28.69m);   // 98.26 - 69.57
        platform.FeeRevenue.Amount.ShouldBe(86.96m);
        platform.NetVatToZatca.Amount.ShouldBe(17.35m);
        platform.ReflectionCount.ShouldBe(1);
    }

    [Fact]
    public void Group_Totals_Equal_Sum_Of_Member_Orders()
    {
        var group = SettlementReports.Group(ScenarioS(), Period);

        group.OrderCount.ShouldBe(3); // reflection excluded (non-financial)
        group.IsBalanced.ShouldBeTrue();
        group.TotalDebits.Amount.ShouldBe(293.00m);
        group.TotalCredits.Amount.ShouldBe(293.00m);

        Credit(group.PerAccount, SettlementAccountCode.ApPartner).ShouldBe(80.00m);   // 70 + 10
        Debit(group.PerAccount, SettlementAccountCode.ArMerchant).ShouldBe(213.00m);  // 100 + 13 + 100
    }

    [Fact]
    public void Partner_Statements_Filter_To_Their_Own_Rows_Only()
    {
        var entries = ScenarioS();

        var a = SettlementReports.Partner(entries, PartnerA, Period);
        a.OrderCount.ShouldBe(1);
        a.TotalPayable.Amount.ShouldBe(70.00m);
        a.OrderRefs.ShouldBe(new[] { "ord-1" });

        var b = SettlementReports.Partner(entries, PartnerB, Period);
        b.OrderCount.ShouldBe(1);
        b.TotalPayable.Amount.ShouldBe(10.00m);
        b.OrderRefs.ShouldBe(new[] { "ord-2" });
    }

    [Fact]
    public void Merchant_Statements_Filter_To_Their_Own_Rows_Only()
    {
        var entries = ScenarioS();

        var a = SettlementReports.Merchant(entries, MerchantA, Period);
        a.OrderCount.ShouldBe(1); // reflection on MerchantA is non-financial → excluded
        a.TotalReceivable.Amount.ShouldBe(100.00m);
        a.TotalFees.Amount.ShouldBe(0m);
        a.OrderRefs.ShouldBe(new[] { "ord-1" });

        var c = SettlementReports.Merchant(entries, MerchantC, Period);
        c.OrderCount.ShouldBe(1);
        c.TotalReceivable.Amount.ShouldBe(100.00m);
        c.TotalFees.Amount.ShouldBe(86.96m);
        c.OrderRefs.ShouldBe(new[] { "ord-3" });
    }

    [Fact]
    public void ReflectionOnly_Contributes_To_No_Financial_Report_Only_A_Count()
    {
        var reflectionOnly = new[] { Reflection };

        var tb = SettlementReports.TrialBalanceFor(reflectionOnly, Period);
        tb.Accounts.ShouldBeEmpty();
        tb.TotalDebits.Amount.ShouldBe(0m);
        tb.TotalCredits.Amount.ShouldBe(0m);
        tb.IsBalanced.ShouldBeTrue();

        var platform = SettlementReports.Platform(reflectionOnly, Period);
        platform.ResaleMargin.Amount.ShouldBe(0m);
        platform.FeeRevenue.Amount.ShouldBe(0m);
        platform.NetVatToZatca.Amount.ShouldBe(0m);
        platform.ReflectionCount.ShouldBe(1);
    }

    [Fact]
    public void Reports_Filter_By_Period()
    {
        var oOldPeriod = SettlementPostingTemplates.Principal(Incl(100m), Incl(70m), Vat)
            .Tag(PartnerA, MerchantA, OtherPeriod, "ord-old");

        var entries = ScenarioS().Append(oOldPeriod).ToArray();

        SettlementReports.Partner(entries, PartnerA, Period).OrderCount.ShouldBe(1); // not ord-old
        SettlementReports.TrialBalanceFor(entries, Period).TotalDebits.Amount.ShouldBe(293.00m);
        SettlementReports.TrialBalanceFor(entries, OtherPeriod).TotalDebits.Amount.ShouldBe(170.00m);
    }

    private static decimal Credit(System.Collections.Generic.IReadOnlyList<AccountBalance> accounts, string code) =>
        accounts.Single(a => a.AccountCode == code).Credit;

    private static decimal Debit(System.Collections.Generic.IReadOnlyList<AccountBalance> accounts, string code) =>
        accounts.Single(a => a.AccountCode == code).Debit;
}
