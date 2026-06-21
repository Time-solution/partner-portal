using System;
using System.Collections.Generic;
using System.Linq;
using Volo.Abp;

namespace Zahy.Settlement;

/// <summary>One order making up the partner payable (drill detail): its net 2100 contribution.</summary>
public sealed record PartnerSettlementStatementLine(string OrderRef, Money Amount);

/// <summary>
/// DIRECTION 2 — a PARTNER SETTLEMENT STATEMENT (Zahy owes the partner / a payable). A SEPARATE output
/// from the invoice (Direction 1) — never merged. Reads owed/disbursed/remaining and the reconciled
/// state from <see cref="DisbursementStatusReport"/> and lists the orders making up the payable. It
/// recomputes no money. BETA-stamped until ZATCA.
/// </summary>
public sealed record PartnerSettlementStatementReport(
    Guid PartnerId,
    SettlementPeriod Period,
    Money OwedToPartner,
    Money DisbursedToDate,
    Money RemainingToDisburse,
    bool IsReconciled,
    DisbursementPositionState State,
    IReadOnlyList<PartnerSettlementStatementLine> Lines)
{
    /// <summary>Always true at this phase — every generated statement is BETA until ZATCA.</summary>
    public bool IsBeta => true;

    public string BetaLabel => SettlementInvoiceConsts.BetaLabel;
}

/// <summary>
/// Builds the <see cref="PartnerSettlementStatementReport"/> read model from the already-computed
/// <see cref="DisbursementStatusReport"/> (owed / disbursed / remaining / position) plus the partner's
/// journal entries for the drill-down lines. No money is recomputed.
/// </summary>
public static class PartnerSettlementStatement
{
    public static PartnerSettlementStatementReport Build(
        Guid partnerId,
        SettlementPeriod period,
        IEnumerable<PostingResult> entries,
        DisbursementStatusReport disbursementStatus)
    {
        Check.NotNull(disbursementStatus, nameof(disbursementStatus));

        var currency = disbursementStatus.AmountOwedToPartner.Currency;

        // The orders making up the payable: net credit to 2100 (AP-Partner) per order ref.
        var lines = (entries ?? Enumerable.Empty<PostingResult>())
            .Where(e => e.IsFinancial && e.PartnerId == partnerId && e.Period == period && e.OrderRef != null)
            .GroupBy(e => e.OrderRef!)
            .Select(g => new PartnerSettlementStatementLine(
                g.Key,
                Money.Of(SettlementMoney.Round(g.Sum(NetPayable)), currency)))
            .Where(l => l.Amount.Amount != 0m)
            .OrderBy(l => l.OrderRef, StringComparer.Ordinal)
            .ToList();

        return new PartnerSettlementStatementReport(
            partnerId,
            period,
            disbursementStatus.AmountOwedToPartner,
            disbursementStatus.DisbursedToDate,
            disbursementStatus.RemainingToDisburse,
            disbursementStatus.State != DisbursementPositionState.NotReconciled,
            disbursementStatus.State,
            lines);
    }

    private static decimal NetPayable(PostingResult entry)
    {
        var credit = entry.LineFor(SettlementAccountCode.ApPartner, EntryDirection.Credit)?.Amount.Amount ?? 0m;
        var debit = entry.LineFor(SettlementAccountCode.ApPartner, EntryDirection.Debit)?.Amount.Amount ?? 0m;
        return credit - debit;
    }
}
