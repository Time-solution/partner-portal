namespace Zahy.Settlement;

/// <summary>
/// The COMMITTED (human) state of a per-partner, per-period reconciliation batch. The engine only
/// proposes (see <see cref="ReconciliationProposedState"/>); a human accountant commits. Reconcile
/// records a verification gate only — it moves no money and posts no journal. The disburse phase
/// Gate-1 passes for BOTH <see cref="Reconciled"/> and <see cref="ReconciledWithOverride"/>.
/// </summary>
public enum ReconciliationState
{
    /// <summary>Not yet committed by a human.</summary>
    Open = 1,

    /// <summary>Committed on a clean (funds-cover) match.</summary>
    Reconciled = 2,

    /// <summary>Committed over an Exception with a mandatory note (audited override).</summary>
    ReconciledWithOverride = 3
}

/// <summary>
/// The engine's PROPOSED state for a match. The engine never self-confirms — it prepares this and the
/// accountant commits. <see cref="Exception"/> can only be committed via an audited override-with-note.
/// </summary>
public enum ReconciliationProposedState
{
    /// <summary>Funds cover the collected side — ready for a clean human commit.</summary>
    ReadyToReconcile = 1,

    /// <summary>Short / mismatch — commit requires a mandatory override note.</summary>
    Exception = 2
}

/// <summary>The outcome of one member in a grouped (partial) reconcile action.</summary>
public enum GroupReconcileOutcome
{
    Reconciled = 1,
    LeftAsException = 2
}
