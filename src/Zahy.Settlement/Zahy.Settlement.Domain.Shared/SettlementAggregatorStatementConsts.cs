namespace Zahy.Settlement;

/// <summary>
/// Lifecycle of an imported aggregator settlement statement.
/// Imported → Matching → (Reconciled | HasExceptions) → Closed.
/// Reconciled is the ENGINE-PROPOSED clean outcome (auto-propose); Closed is the HUMAN commit
/// (two-person gated). HasExceptions closes only after every exception is resolved with a note.
/// </summary>
public enum AggregatorStatementStatus
{
    Imported = 1,
    Matching = 2,
    Reconciled = 3,
    HasExceptions = 4,
    Closed = 5
}

/// <summary>
/// Classified variance between an aggregator statement and our reflected Order Ledger.
/// Zahy is PRINCIPAL with aggregators: the statement fee is OUR BUY SIDE (compared against the
/// buy leg of the existing SettlementCostMarkupSnapshot), never a commission we earn. The gross is
/// ReflectionOnly merchant sales, never Zahy revenue.
/// </summary>
public enum AggregatorVarianceType
{
    /// <summary>Statement line with no reflected order for its external ref.</summary>
    MissingInLedger = 1,

    /// <summary>Reflected order in the period absent from the statement.</summary>
    MissingInStatement = 2,

    /// <summary>Statement gross differs from the reflected order amount beyond tolerance.</summary>
    AmountMismatch = 3,

    /// <summary>Statement aggregator fee differs from the snapshot BUY leg beyond tolerance.</summary>
    FeeVsBuySnapshotMismatch = 4,

    /// <summary>Same externalOrderRef appears more than once in one statement.</summary>
    DuplicateLine = 5,

    /// <summary>Statement-level: declared net does not tie to Σ per-order (gross − fee) via the
    /// existing CodNetTransferred invariant.</summary>
    NetTransferMismatch = 6
}

public static class SettlementAggregatorStatementConsts
{
    /// <summary>
    /// THE single amount-match tolerance (absolute, same currency): |statement − ledger| ≤ tolerance
    /// matches; anything beyond is a variance. Referenced everywhere — never re-typed as a literal.
    /// </summary>
    public const decimal AmountTolerance = 0.01m;

    public const int MaxSourceLength = 64;
    public const int MaxExternalOrderRefLength = 256;   // aligns SettlementCaseConsts.MaxExternalTransactionIdLength
    public const int MaxImportIdempotencyKeyLength = 128; // sha256 hex = 64; headroom mirrors disburse keys
    public const int MaxActorLength = 128;              // aligns MaxReconciledByLength / MaxReleasedByLength
    public const int MaxResolutionNoteLength = 512;     // aligns MaxOverrideReasonLength
    public const int MaxDetailsLength = 512;
}

/// <summary>
/// Aggregator-statement reconcile error codes — range :070–:075.
/// Range evidence at assignment time: :001–:006, :020–:025, :030–:034 (NOTE — :030–:033 are doubly
/// assigned by the pre-existing SettlementPaymentConsts/SettlementVatConsts collision), :040–:048,
/// :050–:058, :061–:062 are occupied; nothing exists at :063+. :070+ is clean with a buffer.
/// None of these move money — this whole area is a compute-only verification gate.
/// </summary>
public static class SettlementAggregatorStatementErrorCodes
{
    public const string Namespace = "Zahy.Settlement";

    /// <summary>Import rejected: no lines, blank source, or an inverted period.</summary>
    public const string StatementImportInvalid = Namespace + ":070";

    /// <summary>Statement lifecycle transition not allowed from the current status.</summary>
    public const string IllegalStatementTransition = Namespace + ":071";

    /// <summary>Reconciled/Closed statements are immutable — corrections are append-only.</summary>
    public const string StatementImmutable = Namespace + ":072";

    /// <summary>Resolving an exception requires a mandatory audit note.</summary>
    public const string ExceptionResolutionRequiresNote = Namespace + ":073";

    /// <summary>A statement cannot close while any exception is unresolved.</summary>
    public const string CloseBlockedOpenExceptions = Namespace + ":074";

    /// <summary>TWO-PERSON rule — whoever resolved any exception on the statement cannot close it
    /// (mirrors reconciler ≠ releaser on disbursement).</summary>
    public const string CloseBlockedSameActorAsResolver = Namespace + ":075";
}
