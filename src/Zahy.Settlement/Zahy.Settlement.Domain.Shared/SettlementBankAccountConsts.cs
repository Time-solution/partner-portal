namespace Zahy.Settlement;

/// <summary>
/// Field limits and status for the accountant-managed bank registry (Track B). Banks are master data
/// the accountant adds via settings; each bank gets a ledger sub-account under the 1100 parent.
/// </summary>
public static class SettlementBankAccountConsts
{
    public const int MaxNameLength = 128;

    /// <summary>Full account / IBAN — stored whole, MASKED for display (see <see cref="BankLedgerCoding.Mask"/>).</summary>
    public const int MaxAccountNumberLength = 64;

    public const int MaxCurrencyLength = 3;

    /// <summary>Optional future gateway-route key (e.g. provider merchant id). SEAM ONLY — not wired.</summary>
    public const int MaxGatewayMappingLength = 128;
}

/// <summary>Whether a registered bank account is usable for new payment routing.</summary>
public enum BankAccountStatus
{
    Active = 1,
    Inactive = 2,
}
