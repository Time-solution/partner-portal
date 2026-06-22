namespace Zahy.Settlement;

/// <summary>
/// Error codes for triggering an append-only SettlementCase reversal (the compensating-entry path).
/// Distinct from <see cref="SettlementDisbursementErrorCodes"/> (disbursement reversal) and the
/// commission ledger reversal — this covers ONLY the settlement-case reversal trigger.
/// </summary>
public static class SettlementReversalErrorCodes
{
    public const string Namespace = "Zahy.Settlement";

    /// <summary>Original case is in a non-reversible state, or has already been reversed.</summary>
    public const string OriginalNotReversible = Namespace + ":061";

    /// <summary>Reversal reason is missing or shorter than the required minimum length.</summary>
    public const string ReasonRequired = Namespace + ":062";

    /// <summary>Minimum length of a reversal reason.</summary>
    public const int MinReasonLength = 10;
}
