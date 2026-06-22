namespace Zahy.Settlement;

/// <summary>
/// Field limits + status for the per-partner ledger registry. Each partner active in settlement is
/// bound to a payable (2101+) and receivable (1251+) sub-account under the 2100/1250 parents.
/// </summary>
public static class SettlementPartnerLedgerConsts
{
    public const int MaxPartnerNameLength = 256;
}

/// <summary>Whether a partner's ledger sub-accounts are usable for new posting routing.</summary>
public enum PartnerLedgerAccountStatus
{
    Active = 1,
    Inactive = 2,
}
