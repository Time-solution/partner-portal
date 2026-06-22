using System.Collections.Generic;

namespace Zahy.Settlement;

/// <summary>
/// Computes the fee posting results for an <see cref="ActivationFeeConfig"/> — COMPUTE ONLY.
/// Trigger semantics:
///   • Subscription fee → once per PERIOD (one result if the line is enabled).
///   • Per-transaction fee → once per SUCCESSFUL transaction (one result each).
/// Each result is built by the payer-selectable <see cref="SettlementPostingTemplates.Fee"/> template
/// (Merchant → 1200, Partner → 1250). Both lines may be ON, producing two independent computations.
/// NOTHING is posted/persisted here: <c>SettlementEngineOptions.PostingEnabled</c> stays OFF, so these
/// are surfaced for the fee preview / bulk-invoice read model only.
/// </summary>
public static class ActivationFeeComputer
{
    /// <summary>
    /// Compute-only default — sourced from the module's canonical VAT config
    /// (<see cref="SettlementVatOptions.DefaultStandardRate"/>) so the rate is not a second hardcoded
    /// literal. Production callers pass the configured <c>SettlementVatOptions.StandardRate</c>.
    /// </summary>
    public const decimal DefaultVatRate = SettlementVatOptions.DefaultStandardRate;

    /// <summary>The single subscription-fee result for the period, or null when the line is disabled.</summary>
    public static PostingResult? Subscription(ActivationFeeConfig config, decimal vatRate = DefaultVatRate)
    {
        var line = config.Subscription;
        return line.Enabled
            ? SettlementPostingTemplates.Fee(line.AmountInclusive, line.Payer, vatRate)
            : null;
    }

    /// <summary>One per-transaction-fee result PER successful transaction (empty when disabled / none).</summary>
    public static IReadOnlyList<PostingResult> PerTransaction(
        ActivationFeeConfig config,
        int successfulTransactionCount,
        decimal vatRate = DefaultVatRate)
    {
        var results = new List<PostingResult>();
        var line = config.PerTransaction;
        if (!line.Enabled || successfulTransactionCount <= 0)
        {
            return results;
        }

        for (var i = 0; i < successfulTransactionCount; i++)
        {
            results.Add(SettlementPostingTemplates.Fee(line.AmountInclusive, line.Payer, vatRate));
        }

        return results;
    }

    /// <summary>
    /// All computed fee results for one activation in a period: the (optional) subscription result
    /// plus one per-transaction result for each successful transaction. The two lines are computed
    /// independently — both ON yields both sets.
    /// </summary>
    public static IReadOnlyList<PostingResult> ComputeForPeriod(
        ActivationFeeConfig config,
        int successfulTransactionCount,
        decimal vatRate = DefaultVatRate)
    {
        var results = new List<PostingResult>();

        var subscription = Subscription(config, vatRate);
        if (subscription != null)
        {
            results.Add(subscription);
        }

        results.AddRange(PerTransaction(config, successfulTransactionCount, vatRate));
        return results;
    }
}
