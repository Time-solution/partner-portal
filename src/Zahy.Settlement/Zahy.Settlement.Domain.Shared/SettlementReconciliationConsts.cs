namespace Zahy.Settlement;

public static class SettlementReconciliationConsts
{
    /// <summary>Who reconciled (audit trail; an accountant/admin user identifier or name).</summary>
    public const int MaxReconciledByLength = 128;

    /// <summary>Override audit note (mandatory when committing over an Exception).</summary>
    public const int MaxOverrideReasonLength = 512;

    /// <summary>Stable reason token surfaced when reconcile is blocked because the money is not yet in.</summary>
    public const string BlockedFundsNotReceived = "ReconcileBlocked_FundsNotReceived";
}

/// <summary>
/// Reconcile error codes. Kept separate from the earlier locked code groups. Reconcile is a
/// verification gate — these signal a BLOCKED reconcile, never a money movement.
/// </summary>
public static class SettlementReconciliationErrorCodes
{
    public const string Namespace = "Zahy.Settlement";

    /// <summary>Funds received do not cover the collected side of the period's orders — the money is
    /// invoiced but not actually in, so reconcile (and therefore any later disbursement) is blocked.</summary>
    public const string ReconcileBlockedFundsNotReceived = Namespace + ":040";

    /// <summary>The match supplied to a batch is for a different partner/period than the batch.</summary>
    public const string ReconcilePartnerPeriodMismatch = Namespace + ":041";

    /// <summary>Committing over an Exception requires a mandatory override note.</summary>
    public const string ReconcileOverrideRequiresNote = Namespace + ":042";
}
