using System;
using System.Collections.Generic;
using System.Linq;

namespace Zahy.Settlement;

/// <summary>Per-account debit/credit totals for a period. Net is debit-positive (Debit − Credit).</summary>
public sealed record AccountBalance(string AccountCode, decimal Debit, decimal Credit)
{
    public decimal Net => SettlementMoney.Round(Debit - Credit);
}

/// <summary>Level 1 — one order/settlement: its journal lines + derived margin and net VAT.</summary>
public sealed record OrderReport(
    string? OrderRef,
    Guid? PartnerId,
    Guid? MerchantId,
    SettlementPeriod? Period,
    ParticipationMode Mode,
    IReadOnlyList<PostingLine> Lines,
    Money Margin,
    Money NetVat,
    bool IsBalanced,
    decimal TrialBalanceNet);

/// <summary>Level 2 — a group of orders (by period, optionally by batch): per-account totals.</summary>
public sealed record OrderGroupReport(
    SettlementPeriod Period,
    string? BatchRef,
    int OrderCount,
    IReadOnlyList<AccountBalance> PerAccount,
    Money TotalDebits,
    Money TotalCredits)
{
    public bool IsBalanced => TotalDebits.Amount == TotalCredits.Amount;

    public decimal TrialBalanceNet => SettlementMoney.Round(TotalDebits.Amount - TotalCredits.Amount);
}

/// <summary>Level 3 — partner settlement statement: their orders + total payable (AP-Partner, 2100).</summary>
public sealed record PartnerStatement(
    Guid PartnerId,
    SettlementPeriod Period,
    int OrderCount,
    Money TotalPayable,
    IReadOnlyList<string> OrderRefs);

/// <summary>Level 4 — merchant settlement statement: their orders + receivable (1200) + fees (4200).</summary>
public sealed record MerchantStatement(
    Guid MerchantId,
    SettlementPeriod Period,
    int OrderCount,
    Money TotalReceivable,
    Money TotalFees,
    IReadOnlyList<string> OrderRefs);

/// <summary>Level 5 — platform report: resale margin (4100−5100), fee revenue (4200), net VAT (2200−1300).</summary>
public sealed record PlatformReport(
    SettlementPeriod Period,
    Money ResaleMargin,
    Money FeeRevenue,
    Money NetVatToZatca,
    int ReflectionCount);

/// <summary>Level 6 — trial balance: per-account balances with the hard debits = credits invariant.</summary>
public sealed record TrialBalance(
    SettlementPeriod Period,
    IReadOnlyList<AccountBalance> Accounts,
    Money TotalDebits,
    Money TotalCredits)
{
    public bool IsBalanced => TotalDebits.Amount == TotalCredits.Amount;

    public decimal Net => SettlementMoney.Round(TotalDebits.Amount - TotalCredits.Amount);
}

/// <summary>Level 7 — VAT control (reporting figure only; no live close posting): output 2200 − input 1300.</summary>
public sealed record VatControlReport(
    SettlementPeriod Period,
    Money OutputVat,
    Money InputVat,
    Money NetVatPayable);

/// <summary>
/// Pure read-model reports over a set of (tagged) <see cref="PostingResult"/> journals. Reflection-only
/// entries carry no financial lines and therefore contribute nothing to any financial figure — only to
/// the reflection count. Every level uses real chart codes and the trial balance nets to zero.
/// </summary>
public static class SettlementReports
{
    public static OrderReport Order(PostingResult entry) =>
        new(
            entry.OrderRef,
            entry.PartnerId,
            entry.MerchantId,
            entry.Period,
            entry.Mode,
            entry.Lines,
            entry.Margin,
            entry.NetVat,
            entry.IsBalanced,
            entry.TrialBalanceNet);

    public static OrderGroupReport Group(
        IEnumerable<PostingResult> entries,
        SettlementPeriod period,
        string? batchRef = null)
    {
        var scoped = Financial(entries, period)
            .Where(e => batchRef == null || e.BatchRef == batchRef)
            .ToList();

        var accounts = Aggregate(scoped);

        return new OrderGroupReport(
            period,
            batchRef,
            scoped.Count,
            accounts,
            Money.Of(accounts.Sum(a => a.Debit)),
            Money.Of(accounts.Sum(a => a.Credit)));
    }

    public static PartnerStatement Partner(IEnumerable<PostingResult> entries, Guid partnerId, SettlementPeriod period)
    {
        var scoped = Financial(entries, period).Where(e => e.PartnerId == partnerId).ToList();
        var accounts = Aggregate(scoped);

        return new PartnerStatement(
            partnerId,
            period,
            scoped.Count,
            Money.Of(NetCredit(accounts, SettlementAccountCode.ApPartner)),
            scoped.Where(e => e.OrderRef != null).Select(e => e.OrderRef!).ToList());
    }

    public static MerchantStatement Merchant(IEnumerable<PostingResult> entries, Guid merchantId, SettlementPeriod period)
    {
        var scoped = Financial(entries, period).Where(e => e.MerchantId == merchantId).ToList();
        var accounts = Aggregate(scoped);

        return new MerchantStatement(
            merchantId,
            period,
            scoped.Count,
            Money.Of(NetDebit(accounts, SettlementAccountCode.ArMerchant)),
            Money.Of(NetCredit(accounts, SettlementAccountCode.FeeRevenue)),
            scoped.Where(e => e.OrderRef != null).Select(e => e.OrderRef!).ToList());
    }

    public static PlatformReport Platform(IEnumerable<PostingResult> entries, SettlementPeriod period)
    {
        var all = entries.Where(e => e.Period == period).ToList();
        var accounts = Aggregate(all.Where(e => e.IsFinancial));

        var resaleMargin = SettlementMoney.Round(
            NetCredit(accounts, SettlementAccountCode.ResaleRevenue) - NetDebit(accounts, SettlementAccountCode.PartnerCogs));
        var netVat = SettlementMoney.Round(
            NetCredit(accounts, SettlementAccountCode.OutputVat) - NetDebit(accounts, SettlementAccountCode.InputVat));

        return new PlatformReport(
            period,
            Money.Of(resaleMargin),
            Money.Of(NetCredit(accounts, SettlementAccountCode.FeeRevenue)),
            Money.Of(netVat),
            all.Count(e => e.Mode == ParticipationMode.ReflectionOnly));
    }

    public static TrialBalance TrialBalanceFor(IEnumerable<PostingResult> entries, SettlementPeriod period)
    {
        var accounts = Aggregate(Financial(entries, period));

        return new TrialBalance(
            period,
            accounts,
            Money.Of(accounts.Sum(a => a.Debit)),
            Money.Of(accounts.Sum(a => a.Credit)));
    }

    public static VatControlReport VatControl(IEnumerable<PostingResult> entries, SettlementPeriod period)
    {
        var accounts = Aggregate(Financial(entries, period));

        var output = NetCredit(accounts, SettlementAccountCode.OutputVat);
        var input = NetDebit(accounts, SettlementAccountCode.InputVat);

        return new VatControlReport(
            period,
            Money.Of(output),
            Money.Of(input),
            Money.Of(SettlementMoney.Round(output - input)));
    }

    private static IEnumerable<PostingResult> Financial(IEnumerable<PostingResult> entries, SettlementPeriod period) =>
        entries.Where(e => e.IsFinancial && e.Period == period);

    private static IReadOnlyList<AccountBalance> Aggregate(IEnumerable<PostingResult> entries)
    {
        var debits = new Dictionary<string, decimal>();
        var credits = new Dictionary<string, decimal>();

        foreach (var line in entries.SelectMany(e => e.Lines))
        {
            var bucket = line.Direction == EntryDirection.Debit ? debits : credits;
            bucket[line.AccountCode] = bucket.GetValueOrDefault(line.AccountCode) + line.Amount.Amount;
        }

        return debits.Keys.Union(credits.Keys)
            .OrderBy(code => code, StringComparer.Ordinal)
            .Select(code => new AccountBalance(
                code,
                SettlementMoney.Round(debits.GetValueOrDefault(code)),
                SettlementMoney.Round(credits.GetValueOrDefault(code))))
            .ToList();
    }

    private static decimal NetCredit(IEnumerable<AccountBalance> accounts, string code)
    {
        var a = accounts.FirstOrDefault(x => x.AccountCode == code);
        return a is null ? 0m : SettlementMoney.Round(a.Credit - a.Debit);
    }

    private static decimal NetDebit(IEnumerable<AccountBalance> accounts, string code)
    {
        var a = accounts.FirstOrDefault(x => x.AccountCode == code);
        return a is null ? 0m : SettlementMoney.Round(a.Debit - a.Credit);
    }
}
