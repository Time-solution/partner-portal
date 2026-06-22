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

    /// <summary>
    /// Track B — gates applying per-bank ledger sub-accounts (110x) to the LIVE production chart of
    /// accounts. OFF until CTO/accountant sign-off (same governance as the 1250 chart change). While
    /// OFF, the bank registry + routing are mock/compute-only and the production chart is NOT mutated.
    /// </summary>
    public bool BankRegistryLiveChartEnabled { get; set; } = false;

    /// <summary>
    /// Per-partner ledger — gates applying per-partner payable/receivable sub-accounts (2101+ under 2100,
    /// 1251+ under 1250) to the LIVE production chart of accounts. OFF until CTO/accountant sign-off (same
    /// governance as the bank sub-accounts). While OFF, the partner ledger registry + posting routing are
    /// mock/compute-only and the production chart is NOT mutated.
    /// </summary>
    public bool PartnerLedgerLiveChartEnabled { get; set; } = false;
}
