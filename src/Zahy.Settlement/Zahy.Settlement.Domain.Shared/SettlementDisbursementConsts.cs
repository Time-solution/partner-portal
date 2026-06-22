namespace Zahy.Settlement;

public static class SettlementDisbursementConsts
{
    /// <summary>Double-submit safety key (unique per disbursement).</summary>
    public const int MaxIdempotencyKeyLength = 128;

    /// <summary>Who released the lock (audit trail; a disburse-privileged user identifier or name).</summary>
    public const int MaxReleasedByLength = 128;
}

/// <summary>
/// Disburse error codes. Kept separate from the earlier locked code groups. Each gate BLOCKS a
/// disbursement with a clear reason; none of these move money.
/// </summary>
public static class SettlementDisbursementErrorCodes
{
    public const string Namespace = "Zahy.Settlement";

    /// <summary>Gate 1 — the partner+period is not Reconciled.</summary>
    public const string DisburseBlockedNotReconciled = Namespace + ":050";

    /// <summary>Gate 2 — would pay out more than the funds actually received for the period.</summary>
    public const string DisburseBlockedExceedsFundsReceived = Namespace + ":051";

    /// <summary>Gate 3 — would pay out more than the partner payable (net 2100).</summary>
    public const string DisburseBlockedExceedsPayable = Namespace + ":052";

    /// <summary>Human-release lock — the actor lacks the Disburse privilege (separation of duties).</summary>
    public const string DisburseBlockedNotAuthorized = Namespace + ":053";

    public const string NonPositiveDisbursement = Namespace + ":054";

    public const string EmptyIdempotencyKey = Namespace + ":055";

    public const string InvalidReversalLink = Namespace + ":056";

    /// <summary>
    /// Two-person rule — the human RELEASING a payout is the SAME person who RECONCILED the batch. Even a
    /// PlatformAdmin holding both privileges is blocked from doing both gates on the same item.
    /// </summary>
    public const string DisburseBlockedSameActorAsReconciler = Namespace + ":057";

    /// <summary>A reversal was requested for a disbursement that is not in the Released state.</summary>
    public const string DisburseReversalRequiresReleased = Namespace + ":058";
}
