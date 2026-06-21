namespace Zahy.Settlement;

/// <summary>
/// Engine feature flags — all real-world execution OFF until accountant + CTO sign-off. P4 ingestion
/// never advances a case past Allocated and never moves money; these gate the later phases.
/// </summary>
public class SettlementEngineOptions
{
    public const string SectionName = "Settlement:Engine";

    public bool DisbursementEnabled { get; set; } = false;

    public bool LiveProviderEnabled { get; set; } = false;

    /// <summary>
    /// Gates persisting/applying financial postings (the Principal / SubscriptionFee templates).
    /// OFF until accountant + CTO sign-off — templates may be computed, but nothing is posted/booked.
    /// </summary>
    public bool PostingEnabled { get; set; } = false;
}
