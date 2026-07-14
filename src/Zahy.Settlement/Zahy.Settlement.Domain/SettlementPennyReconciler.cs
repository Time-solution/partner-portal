using System;

namespace Zahy.Settlement;

/// <summary>
/// F16 — THE largest-remainder penny reconciliation (extracted from the allocator so the rule is
/// n-way testable). RULE: round each leg once; then distribute the residual between the rounded sum
/// and the total ONE CENT PER STEP, each step applied to the largest leg that stays ≥ 0 (ties broken
/// by lowest index — deterministic). Guarantees sum(parts) == round(total) at 2dp with no negative
/// part. Unchanged behavior at the edges: a zero residual returns the rounded legs as-is (clean
/// inputs byte-identical), a single-cent residual still lands on the largest leg (the pinned
/// allocator behavior), and a residual beyond one cent per leg is a genuine imbalance returned
/// unreconciled for the caller's balance invariant to reject.
/// </summary>
public static class SettlementPennyReconciler
{
    private const decimal Cent = 0.01m;

    public static decimal[] Reconcile(decimal[] legs, decimal total)
    {
        var rounded = new decimal[legs.Length];
        var sum = 0m;
        for (var i = 0; i < legs.Length; i++)
        {
            rounded[i] = SettlementMoney.Round(legs[i]);
            sum += rounded[i];
        }

        var residual = SettlementMoney.Round(SettlementMoney.Round(total) - sum);
        if (residual == 0m)
        {
            return rounded;
        }

        var pennyTolerance = Cent * legs.Length;
        if (Math.Abs(residual) > pennyTolerance)
        {
            return rounded; // genuine imbalance — surfaced to the caller's invariant, not masked
        }

        var step = residual > 0m ? Cent : -Cent;
        while (residual != 0m)
        {
            var target = -1;
            for (var i = 0; i < rounded.Length; i++)
            {
                // Eligible = stays non-negative after the step; pick the LARGEST such leg,
                // first index winning ties (strict > keeps the ordering deterministic).
                if (rounded[i] + step < 0m)
                {
                    continue;
                }

                if (target < 0 || rounded[i] > rounded[target])
                {
                    target = i;
                }
            }

            if (target < 0)
            {
                break; // nothing can absorb another cent — leave the rest to the caller's invariant
            }

            rounded[target] = SettlementMoney.Round(rounded[target] + step);
            residual = SettlementMoney.Round(residual - step);
        }

        return rounded;
    }
}
