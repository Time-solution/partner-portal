namespace Zahy.Settlement;

/// <summary>
/// The human-release state of one disbursement row. Created LOCKED by default — even when all three
/// system gates pass, money is not posted/released until an authorized human with the Disburse
/// privilege explicitly releases it (separation of duties). Append-only: corrections are new reversal
/// rows, never edits.
/// </summary>
public enum DisbursementState
{
    Locked = 1,
    Released = 2
}

/// <summary>
/// Derived position of a partner+period payout (read model only), from owed / received / disbursed.
/// </summary>
public enum DisbursementPositionState
{
    /// <summary>The period is not reconciled yet — disbursement is blocked at gate 1.</summary>
    NotReconciled = 1,

    /// <summary>Reconciled, nothing disbursed yet, headroom remains.</summary>
    ReadyToDisburse = 2,

    /// <summary>Some disbursed, headroom still remains.</summary>
    PartiallyDisbursed = 3,

    /// <summary>Disbursed up to the disbursable ceiling (min(owed, received)); nothing remains.</summary>
    FullyDisbursed = 4
}
