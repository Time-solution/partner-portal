using System;

namespace Zahy.PartnerCatalog;

/// <summary>
/// Gate 2b — service order lifecycle. Draft → RequirementsSubmitted → InProgress → Delivered →
/// Accepted → Closed, with MerchantCancelled / PartnerDeclined before InProgress and
/// revision-request looping Delivered → InProgress. COMPUTE-ONLY: no posting, no invoice, no
/// payment, no escrow, no settlement wiring — the order only SNAPSHOTS what settlement will read.
/// </summary>
public enum ServiceOrderStatus
{
    Draft = 1,
    RequirementsSubmitted = 2,
    /// <summary>Partner accepted — work in progress.</summary>
    InProgress = 3,
    Delivered = 4,
    Accepted = 5,
    Closed = 6,
    MerchantCancelled = 7,
    PartnerDeclined = 8
}

/// <summary>What happened on a history row (the append-only Order Ledger versions pattern).</summary>
public enum ServiceOrderAction
{
    Created = 1,
    RequirementsSubmitted = 2,
    PartnerAccepted = 3,
    PartnerDeclined = 4,
    MerchantCancelled = 5,
    Delivered = 6,
    MerchantAccepted = 7,
    AutoAccepted = 8,
    RevisionRequested = 9,
    Closed = 10
}

public static class PartnerCatalogServiceOrderConsts
{
    /// <summary>The Salla calibration: a delivered order auto-accepts after exactly this window.</summary>
    public static readonly TimeSpan AutoAcceptAfter = TimeSpan.FromDays(7);

    /// <summary>Actor recorded on auto-accept history rows.</summary>
    public const string SystemActor = "System";

    // Requirement answers (typed) — caps per answer type.
    public const int MaxShortTextAnswerLength = 150;
    public const int MaxLongTextAnswerLength = 2000;
    public const int MaxLinkAnswerLength = 500;
    /// <summary>FileUpload = declared file NAME reference only — NO storage/upload in this gate.</summary>
    public const int MaxFileNameReferenceLength = 200;

    // Milestones (declared payment plan — DATA ONLY; execution belongs to the escrow gate).
    public const decimal MilestoneMinimumOrderPrice = 1000m;
    public const int MinMilestones = 2;
    public const int MaxMilestones = 5;
    /// <summary>First milestone must carry at least this share of the order price.</summary>
    public const decimal MilestoneFirstMinimumShare = 0.40m;
    public const int MaxMilestoneTitleLength = 150;

    public const int MaxNoteLength = 512;      // aligns the reconcile/override note caps
    public const int MaxActorLength = 128;     // aligns MaxReconciledByLength etc.
}

/// <summary>
/// Service-order error codes — range :060–:071 in the Zahy.PartnerCatalog namespace.
/// Range evidence at assignment time: :001–:038 (PartnerCatalogConsts) and :050–:056
/// (PartnerCatalogListingErrorCodes) are occupied; nothing exists at :039–:049 (2a's buffer) or
/// :057+. :060+ is taken leaving :057–:059 as buffer — same no-adjacent-reuse discipline that
/// avoids the known Settlement Payment/Vat :030–:033 collision class.
/// </summary>
public static class PartnerCatalogServiceOrderErrorCodes
{
    public const string Namespace = "Zahy.PartnerCatalog";

    /// <summary>Transition not allowed from the current status.</summary>
    public const string IllegalOrderTransition = Namespace + ":060";

    /// <summary>Closed / MerchantCancelled / PartnerDeclined are terminal — immutable.</summary>
    public const string OrderTerminalImmutable = Namespace + ":061";

    /// <summary>Decline and revision-request require a mandatory note.</summary>
    public const string OrderNoteRequired = Namespace + ":062";

    /// <summary>Submission requires a non-blank answer for every listing requirement.</summary>
    public const string AnswerMissingForRequirement = Namespace + ":063";

    /// <summary>An answer exceeds its type's cap (field named in the error data).</summary>
    public const string AnswerTooLong = Namespace + ":064";

    /// <summary>A MultiChoice answer is not one of the authored choices.</summary>
    public const string InvalidMultiChoiceAnswer = Namespace + ":065";

    /// <summary>Milestone plans are only allowed when the merchant-facing price ≥ 1000 SAR.</summary>
    public const string MilestonesNotAllowedBelowMinimum = Namespace + ":066";

    /// <summary>A milestone plan must have 2–5 rows.</summary>
    public const string MilestoneRowCountInvalid = Namespace + ":067";

    /// <summary>The first milestone must be ≥ 40% of the order price.</summary>
    public const string MilestoneFirstShareTooSmall = Namespace + ":068";

    /// <summary>Σ milestone amounts must equal the order price exactly (existing money rounding).</summary>
    public const string MilestoneSumMismatch = Namespace + ":069";

    /// <summary>A milestone row is invalid (blank/over-cap title or non-positive amount).</summary>
    public const string MilestoneRowInvalid = Namespace + ":070";

    /// <summary>Orders apply to service offerings (ServiceOneOff / ServiceSubscription) with a
    /// money-bearing participation mode — never ReflectionOnly or non-service kinds.</summary>
    public const string OfferingNotOrderable = Namespace + ":071";
}
