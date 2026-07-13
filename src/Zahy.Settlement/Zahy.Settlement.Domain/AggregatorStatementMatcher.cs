using System;
using System.Collections.Generic;
using System.Linq;
using Volo.Abp;

namespace Zahy.Settlement;

/// <summary>
/// A reflected order as seen by statement matching: the Connectors-phase external-id mapping
/// (ReflectedPartnerOrder.ExternalTransactionId / OrderRecord.SourceOrderId) joined with the
/// reflected Order Ledger gross and the BUY leg of the order's SettlementCostMarkupSnapshot.
/// A VIEW over existing rows — never a new mapping table.
/// </summary>
public sealed record ReflectedOrderMatchView(
    string ExternalOrderRef,
    DateTime OrderDate,
    Money Gross,
    Money AggregatorBuyFee);

/// <summary>An engine-PROPOSED variance (uncommitted — humans resolve committed exception rows).</summary>
public sealed record AggregatorVarianceProposal(
    AggregatorVarianceType Type,
    Guid? StatementLineId,
    string ExternalOrderRef,
    decimal? ExpectedAmount,
    decimal? ActualAmount,
    string Details);

/// <summary>The outcome of matching one statement: matched line ids + proposed variances.</summary>
public sealed record AggregatorMatchOutcome(
    IReadOnlyList<Guid> MatchedLineIds,
    IReadOnlyList<AggregatorVarianceProposal> Variances,
    decimal ComputedNetTransferred)
{
    public bool HasVariances => Variances.Count > 0;
}

/// <summary>
/// Pure statement-vs-ledger matching (mirrors the Reconciliation static style: compute-only, no
/// persistence, no journal, no money movement, mutates nothing it reads).
///
/// Match rule: externalOrderRef EXACT (trimmed, ordinal-ignore-case) + amounts within
/// <see cref="SettlementAggregatorStatementConsts.AmountTolerance"/> (the single tolerance source).
/// Gross compares statement → reflected Order Ledger amount; fee compares statement → snapshot BUY
/// leg (Zahy is principal — the aggregator fee is our buy side, never a commission we earn).
///
/// Statement-level tie-out reuses the EXISTING money seams — zero new money math:
/// per-order net = <see cref="SettlementFourWaySplit.CodNetTransferred"/>(gross, fee); the sum is
/// rounded by <see cref="SettlementMoney.Round"/> and must EQUAL declared net (an invariant, so
/// strict equality after rounding — not the fuzzy amount tolerance). A violating declared net is a
/// NetTransferMismatch exception, never a reason to adjust our numbers.
/// </summary>
public static class AggregatorStatementMatcher
{
    public static AggregatorMatchOutcome Match(
        AggregatorStatement statement,
        IReadOnlyList<AggregatorStatementLine> lines,
        IReadOnlyList<ReflectedOrderMatchView> reflectedOrders)
    {
        Check.NotNull(statement, nameof(statement));
        var lineList = lines ?? Array.Empty<AggregatorStatementLine>();
        var ledger = reflectedOrders ?? Array.Empty<ReflectedOrderMatchView>();

        var variances = new List<AggregatorVarianceProposal>();
        var matched = new List<Guid>();

        var ledgerByRef = ledger
            .GroupBy(v => Normalize(v.ExternalOrderRef))
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

        // Duplicate detection: the FIRST occurrence of a ref participates in matching; every later
        // occurrence is a DuplicateLine variance and is excluded from amount comparison.
        var seenRefs = new HashSet<string>(StringComparer.Ordinal);
        var uniqueLines = new List<AggregatorStatementLine>();

        foreach (var line in lineList)
        {
            var key = Normalize(line.ExternalOrderRef);
            if (!seenRefs.Add(key))
            {
                variances.Add(new AggregatorVarianceProposal(
                    AggregatorVarianceType.DuplicateLine,
                    line.Id,
                    line.ExternalOrderRef,
                    ExpectedAmount: null,
                    ActualAmount: line.Gross,
                    Details: "Same externalOrderRef appears more than once in this statement."));
                continue;
            }

            uniqueLines.Add(line);
        }

        foreach (var line in uniqueLines)
        {
            if (!ledgerByRef.TryGetValue(Normalize(line.ExternalOrderRef), out var view))
            {
                variances.Add(new AggregatorVarianceProposal(
                    AggregatorVarianceType.MissingInLedger,
                    line.Id,
                    line.ExternalOrderRef,
                    ExpectedAmount: null,
                    ActualAmount: line.Gross,
                    Details: "No reflected order found for this external ref."));
                continue;
            }

            var lineVarianceCount = variances.Count;

            if (!WithinTolerance(line.Gross, view.Gross.Amount))
            {
                variances.Add(new AggregatorVarianceProposal(
                    AggregatorVarianceType.AmountMismatch,
                    line.Id,
                    line.ExternalOrderRef,
                    ExpectedAmount: view.Gross.Amount,
                    ActualAmount: line.Gross,
                    Details: "Statement gross differs from the reflected order amount beyond tolerance."));
            }

            if (!WithinTolerance(line.AggregatorFee, view.AggregatorBuyFee.Amount))
            {
                variances.Add(new AggregatorVarianceProposal(
                    AggregatorVarianceType.FeeVsBuySnapshotMismatch,
                    line.Id,
                    line.ExternalOrderRef,
                    ExpectedAmount: view.AggregatorBuyFee.Amount,
                    ActualAmount: line.AggregatorFee,
                    Details: "Statement fee differs from the cost/markup snapshot BUY leg beyond tolerance."));
            }

            if (variances.Count == lineVarianceCount)
            {
                matched.Add(line.Id);
            }
        }

        // Reflected orders in the period that never appear on the statement.
        var statementRefs = new HashSet<string>(lineList.Select(l => Normalize(l.ExternalOrderRef)), StringComparer.Ordinal);
        foreach (var view in ledger.Where(v => !statementRefs.Contains(Normalize(v.ExternalOrderRef))))
        {
            variances.Add(new AggregatorVarianceProposal(
                AggregatorVarianceType.MissingInStatement,
                StatementLineId: null,
                view.ExternalOrderRef,
                ExpectedAmount: view.Gross.Amount,
                ActualAmount: null,
                Details: "Reflected order in the period is absent from the statement."));
        }

        // Statement-level tie-out via the EXISTING CodNetTransferred seam (collected − fee per order).
        var computedNet = SettlementMoney.Round(uniqueLines.Sum(line =>
            SettlementFourWaySplit.CodNetTransferred(
                Money.Of(line.Gross, statement.Currency),
                Money.Of(line.AggregatorFee, statement.Currency))));

        if (computedNet != SettlementMoney.Round(statement.DeclaredNet))
        {
            variances.Add(new AggregatorVarianceProposal(
                AggregatorVarianceType.NetTransferMismatch,
                StatementLineId: null,
                ExternalOrderRef: string.Empty,
                ExpectedAmount: computedNet,
                ActualAmount: statement.DeclaredNet,
                Details: "Declared net does not equal Σ per-order (gross − fee) via CodNetTransferred."));
        }

        return new AggregatorMatchOutcome(matched, variances, computedNet);
    }

    private static bool WithinTolerance(decimal statementAmount, decimal ledgerAmount) =>
        Math.Abs(SettlementMoney.Round(statementAmount) - SettlementMoney.Round(ledgerAmount))
            <= SettlementAggregatorStatementConsts.AmountTolerance;

    private static string Normalize(string reference) =>
        (reference ?? string.Empty).Trim().ToLowerInvariant();
}
