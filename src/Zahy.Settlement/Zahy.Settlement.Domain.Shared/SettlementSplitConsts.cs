namespace Zahy.Settlement;

/// <summary>
/// Error codes for the canonical FOUR-way split + COD reconciliation invariants (ported from the frontend
/// types.ts so the server enforces them). Code numbers continue the Settlement series (044/045 webhook,
/// 050–053 disbursement, etc.).
/// </summary>
public static class SettlementSplitErrorCodes
{
    public const string Namespace = "Zahy.Settlement";

    /// <summary>Collected ≠ Merchant + Delivery + ZahyMargin + NetVAT.</summary>
    public const string FourWaySplitDoesNotBalance = Namespace + ":046";

    /// <summary>Seeded COD "net transferred" drifts from the computed (collected − delivery fee).</summary>
    public const string CodNetTransferMismatch = Namespace + ":047";

    /// <summary>COD net transferred ≠ Merchant + ZahyMargin + NetVAT (a stray/unaccounted amount).</summary>
    public const string CodUnaccountedAmount = Namespace + ":048";
}
