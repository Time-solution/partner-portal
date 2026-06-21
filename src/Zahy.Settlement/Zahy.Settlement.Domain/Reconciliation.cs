using System;
using System.Collections.Generic;
using System.Linq;

namespace Zahy.Settlement;

/// <summary>
/// The core reconcile check for one partner+period, computed from the EXISTING read models — money is
/// never recomputed here. <see cref="AmountOwedToPartner"/> reads the partner payable (net 2100) and
/// <see cref="FundsReceived"/> sums payments RECEIVED against the partner's order refs (via
/// <see cref="PaymentLedger"/>). Reconcile is allowed only when the funds actually IN cover the
/// collected side of those orders (<see cref="CollectedExpected"/> = AR booked, 1200 + 1250 debits).
/// </summary>
public sealed record ReconciliationMatch(
    Guid PartnerId,
    SettlementPeriod Period,
    Money AmountOwedToPartner,
    Money FundsReceived,
    Money CollectedExpected)
{
    /// <summary>True when the money is actually in: funds received ≥ the collected side of the orders.</summary>
    public bool FundsCovered => FundsReceived.Amount >= CollectedExpected.Amount;

    /// <summary>
    /// The engine's PROPOSED state — never auto-committed. ReadyToReconcile when funds cover; Exception
    /// (short / mismatch) otherwise, which can only be committed via an audited override-with-note.
    /// </summary>
    public ReconciliationProposedState ProposedState =>
        FundsCovered ? ReconciliationProposedState.ReadyToReconcile : ReconciliationProposedState.Exception;

    /// <summary>Set only when reconcile is blocked (funds short / not received). Null when covered.</summary>
    public string? BlockedReason => FundsCovered ? null : SettlementReconciliationConsts.BlockedFundsNotReceived;
}

/// <summary>
/// Read model: the reconcile status of one partner+period. Carries BOTH the engine's
/// <see cref="ProposedState"/> (ReadyToReconcile|Exception) and the human <see cref="CommittedState"/>
/// (Open|Reconciled|ReconciledWithOverride), plus the override audit when one was applied.
/// </summary>
public sealed record ReconciliationStatusReport(
    Guid PartnerId,
    SettlementPeriod Period,
    Money AmountOwedToPartner,
    Money FundsReceived,
    Money CollectedExpected,
    ReconciliationProposedState ProposedState,
    ReconciliationState CommittedState,
    bool IsReconciled,
    string? BlockedReason,
    string? OverrideReason,
    string? OverrideBy);

/// <summary>
/// Pure reconcile logic over tagged <see cref="PostingResult"/> journals and <see cref="Payment"/>
/// receipts. Computes the per-partner/period match and the status read model. It records/derives a
/// verification gate ONLY — it posts no journal and moves no money.
/// </summary>
public static class Reconciliation
{
    /// <summary>
    /// The core check for a partner+period: partner payable (net 2100), funds received (payments
    /// against the partner's order refs) and the collected side (AR booked, 1200 + 1250 debits).
    /// </summary>
    public static ReconciliationMatch Match(
        Guid partnerId,
        SettlementPeriod period,
        IEnumerable<PostingResult> entries,
        IEnumerable<Payment> payments)
    {
        var all = entries?.ToList() ?? new List<PostingResult>();
        var paymentList = payments?.ToList() ?? new List<Payment>();

        // The partner's financial orders for the period (reflection-only entries carry no lines).
        var scoped = all
            .Where(e => e.IsFinancial && e.PartnerId == partnerId && e.Period == period)
            .ToList();

        var currency = ResolveCurrency(scoped);

        // What Zahy owes the partner (net 2100) — read straight from the partner statement.
        var amountOwed = SettlementReports.Partner(all, partnerId, period).TotalPayable;

        // The collected side that must turn into cash: AR booked on these orders (1200 + 1250 debits).
        var collectedExpected = SettlementMoney.Round(
            scoped.SelectMany(e => e.Lines)
                .Where(l => l.Direction == EntryDirection.Debit && IsReceivable(l.AccountCode))
                .Sum(l => l.Amount.Amount));

        // Funds actually received that fund those orders — summed per ref via the payment ledger.
        var partnerRefs = scoped
            .Where(e => e.OrderRef != null)
            .Select(e => e.OrderRef!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var fundsReceived = SettlementMoney.Round(
            partnerRefs.Sum(reference => PaymentLedger.PaidToDate(reference, paymentList)));

        return new ReconciliationMatch(
            partnerId,
            period,
            amountOwed,
            Money.Of(fundsReceived, currency),
            Money.Of(collectedExpected, currency));
    }

    /// <summary>
    /// Status read model combining the computed match with the persisted batch state. When no batch
    /// exists yet the period is treated as not-reconciled (Open). No money is moved.
    /// </summary>
    public static ReconciliationStatusReport Status(
        Guid partnerId,
        SettlementPeriod period,
        IEnumerable<PostingResult> entries,
        IEnumerable<Payment> payments,
        ReconciliationBatch? batch = null)
    {
        var match = Match(partnerId, period, entries, payments);
        return Status(match, batch);
    }

    public static ReconciliationStatusReport Status(ReconciliationMatch match, ReconciliationBatch? batch = null)
    {
        var committedState = batch?.State ?? ReconciliationState.Open;
        var isReconciled = batch?.IsReconciled ?? false;

        return new ReconciliationStatusReport(
            match.PartnerId,
            match.Period,
            match.AmountOwedToPartner,
            match.FundsReceived,
            match.CollectedExpected,
            match.ProposedState,
            committedState,
            isReconciled,
            isReconciled ? null : match.BlockedReason,
            batch?.OverrideReason,
            batch?.OverrideBy);
    }

    private static bool IsReceivable(string accountCode) =>
        accountCode == SettlementAccountCode.ArMerchant || accountCode == SettlementAccountCode.ArPartner;

    private static string ResolveCurrency(IReadOnlyList<PostingResult> scoped)
    {
        foreach (var entry in scoped)
        {
            if (entry.Lines.Count > 0)
            {
                return entry.Lines[0].Amount.Currency;
            }
        }

        return SettlementConsts.DefaultCurrency;
    }
}
