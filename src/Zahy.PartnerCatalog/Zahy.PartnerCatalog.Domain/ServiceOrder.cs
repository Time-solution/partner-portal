using System;
using System.Collections.Generic;
using System.Linq;
using Volo.Abp;
using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;
using Zahy.Settlement;

namespace Zahy.PartnerCatalog;

/// <summary>
/// Gate 2b — one merchant order on a service offering. COMPUTE-ONLY: no posting, no invoice, no
/// payment, no escrow, no settlement wiring. The order SNAPSHOTS at creation everything the later
/// settlement wiring will read (the SettlementCostMarkupSnapshot precedent — an immutable pair):
/// Principal → buy + sell; SubscriptionFee → fee. Later offering/activation repricing never touches
/// existing orders. Status history is APPEND-ONLY (the Order Ledger versions pattern) — rows are
/// added, never overwritten. Terminal states (Closed / MerchantCancelled / PartnerDeclined) are
/// immutable. Milestones are a DECLARED payment plan only — data, no execution statuses.
/// Tenant-scoped via IMultiTenant (merchant) + partner query filter (partner) — cross-scope access
/// is structurally invisible (the ratified 2a convention).
/// </summary>
public class ServiceOrder : AggregateRoot<Guid>, IMultiTenant
{
    private readonly List<ServiceOrderAnswer> _answers = new();
    private readonly List<ServiceOrderMilestone> _milestones = new();
    private readonly List<ServiceOrderHistoryEntry> _history = new();

    public Guid? TenantId { get; private set; }

    public Guid PartnerId { get; private set; }

    public Guid PartnerCatalogItemId { get; private set; }

    /// <summary>Offering name at order time (display snapshot).</summary>
    public string OfferingNameSnapshot { get; private set; } = string.Empty;

    public ServiceOrderStatus Status { get; private set; }

    // ---- money snapshots (immutable at creation; what settlement wiring reads later) ------------
    public SettlementParticipationMode ParticipationModeSnapshot { get; private set; }

    /// <summary>Partner buy leg (Principal). Null under SubscriptionFee.</summary>
    public decimal? BuySnapshotAmount { get; private set; }

    /// <summary>Merchant sell leg (Principal). Null under SubscriptionFee.</summary>
    public decimal? SellSnapshotAmount { get; private set; }

    /// <summary>Merchant fee (SubscriptionFee). Null under Principal.</summary>
    public decimal? FeeSnapshotAmount { get; private set; }

    public string Currency { get; private set; } = PartnerCatalogConsts.DefaultCurrency;

    public bool PriceVatInclusive { get; private set; }

    public int RevisionCount { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public DateTime? DeliveredAt { get; private set; }

    public DateTime? ClosedAt { get; private set; }

    public IReadOnlyList<ServiceOrderAnswer> Answers => _answers;

    public IReadOnlyList<ServiceOrderMilestone> Milestones => _milestones;

    public IReadOnlyList<ServiceOrderHistoryEntry> History => _history;

    /// <summary>The merchant-facing price: sell under Principal, fee under SubscriptionFee.</summary>
    public decimal MerchantPriceAmount => SellSnapshotAmount ?? FeeSnapshotAmount ?? 0m;

    public bool IsTerminal =>
        Status is ServiceOrderStatus.Closed or ServiceOrderStatus.MerchantCancelled or ServiceOrderStatus.PartnerDeclined;

    protected ServiceOrder()
    {
    }

    private ServiceOrder(Guid id, Guid tenantId, Guid partnerId, Guid partnerCatalogItemId)
        : base(id)
    {
        TenantId = tenantId;
        PartnerId = partnerId;
        PartnerCatalogItemId = partnerCatalogItemId;
    }

    public static ServiceOrder Create(
        Guid id,
        Guid tenantId,
        PartnerCatalogItem offering,
        Money merchantPrice,
        IReadOnlyList<ServiceOrderMilestoneInput>? milestones,
        string actor,
        DateTime now)
    {
        Check.NotNull(offering, nameof(offering));
        Check.NotNull(merchantPrice, nameof(merchantPrice));

        if (!PartnerCatalogAuthoringPolicy.IsSelfServiceOfferingKind(offering.OfferingKind) ||
            offering.SettlementParticipationMode == SettlementParticipationMode.ReflectionOnly)
        {
            throw new BusinessException(PartnerCatalogServiceOrderErrorCodes.OfferingNotOrderable)
                .WithData("OfferingKind", offering.OfferingKind.ToString())
                .WithData("ParticipationMode", offering.SettlementParticipationMode.ToString());
        }

        var order = new ServiceOrder(id, tenantId, offering.PartnerId, offering.Id)
        {
            OfferingNameSnapshot = offering.Name,
            ParticipationModeSnapshot = offering.SettlementParticipationMode,
            Currency = merchantPrice.Currency,
            PriceVatInclusive = merchantPrice.VatInclusive,
            Status = ServiceOrderStatus.Draft,
            CreatedAt = now
        };

        // Immutable snapshot pair (the SettlementCostMarkupSnapshot precedent):
        // Principal → buy + sell; SubscriptionFee → fee. Copied SCALARS — later repricing of the
        // offering or activation can never reach into an existing order.
        if (offering.SettlementParticipationMode == SettlementParticipationMode.Principal)
        {
            order.BuySnapshotAmount = SettlementMoney.Round(offering.PartnerCost.Amount);
            order.SellSnapshotAmount = SettlementMoney.Round(merchantPrice.Amount);
        }
        else
        {
            order.FeeSnapshotAmount = SettlementMoney.Round(merchantPrice.Amount);
        }

        order.ApplyMilestones(milestones);
        order.Append(ServiceOrderAction.Created, from: null, ServiceOrderStatus.Draft, actor, note: null, now);
        return order;
    }

    // ---- requirements intake ---------------------------------------------------------------------

    /// <summary>
    /// Merchant answers every listing requirement (typed caps; MultiChoice must pick an authored
    /// choice; FileUpload = declared file NAME only — no storage). Each answer SNAPSHOTS the
    /// requirement title at answer time: the listing may change later, the order keeps what the
    /// merchant saw. The :056 anti-disintermediation policy deliberately does NOT apply here —
    /// the buyer's own info is theirs to share.
    /// </summary>
    public void SubmitRequirements(
        IReadOnlyList<ListingRequirement> listingRequirements,
        IReadOnlyDictionary<int, string> answersByRequirementOrderIndex,
        string actor,
        DateTime now)
    {
        EnsureTransition(ServiceOrderStatus.Draft, ServiceOrderStatus.RequirementsSubmitted);

        var requirements = (listingRequirements ?? Array.Empty<ListingRequirement>())
            .OrderBy(r => r.OrderIndex)
            .ToList();

        var answers = new List<ServiceOrderAnswer>(requirements.Count);
        foreach (var requirement in requirements)
        {
            if (!answersByRequirementOrderIndex.TryGetValue(requirement.OrderIndex, out var raw) ||
                string.IsNullOrWhiteSpace(raw))
            {
                throw new BusinessException(PartnerCatalogServiceOrderErrorCodes.AnswerMissingForRequirement)
                    .WithData("RequirementTitle", requirement.Title);
            }

            var answer = raw.Trim();
            var cap = requirement.Type switch
            {
                ListingRequirementType.ShortText => PartnerCatalogServiceOrderConsts.MaxShortTextAnswerLength,
                ListingRequirementType.LongText => PartnerCatalogServiceOrderConsts.MaxLongTextAnswerLength,
                ListingRequirementType.Link => PartnerCatalogServiceOrderConsts.MaxLinkAnswerLength,
                ListingRequirementType.FileUpload => PartnerCatalogServiceOrderConsts.MaxFileNameReferenceLength,
                _ => PartnerCatalogServiceOrderConsts.MaxShortTextAnswerLength
            };

            if (requirement.Type == ListingRequirementType.MultiChoice)
            {
                if (!requirement.Choices.Contains(answer, StringComparer.Ordinal))
                {
                    throw new BusinessException(PartnerCatalogServiceOrderErrorCodes.InvalidMultiChoiceAnswer)
                        .WithData("RequirementTitle", requirement.Title)
                        .WithData("Answer", answer);
                }
            }
            else if (answer.Length > cap)
            {
                throw new BusinessException(PartnerCatalogServiceOrderErrorCodes.AnswerTooLong)
                    .WithData("RequirementTitle", requirement.Title)
                    .WithData("Type", requirement.Type.ToString())
                    .WithData("Cap", cap);
            }

            answers.Add(new ServiceOrderAnswer(
                Guid.NewGuid(), answers.Count, requirement.Title, requirement.Type, answer));
        }

        _answers.Clear();
        _answers.AddRange(answers);
        Transition(ServiceOrderAction.RequirementsSubmitted, ServiceOrderStatus.RequirementsSubmitted, actor, null, now);
    }

    // ---- lifecycle ---------------------------------------------------------------------------------

    public void PartnerAccept(string actor, DateTime now)
    {
        EnsureTransition(ServiceOrderStatus.RequirementsSubmitted, ServiceOrderStatus.InProgress);
        Transition(ServiceOrderAction.PartnerAccepted, ServiceOrderStatus.InProgress, actor, null, now);
    }

    /// <summary>Only before InProgress; mandatory note.</summary>
    public void PartnerDecline(string actor, string note, DateTime now)
    {
        RequireNote(note);
        EnsureBeforeInProgress();
        Transition(ServiceOrderAction.PartnerDeclined, ServiceOrderStatus.PartnerDeclined, actor, note.Trim(), now);
    }

    /// <summary>Only before InProgress.</summary>
    public void MerchantCancel(string actor, DateTime now)
    {
        EnsureBeforeInProgress();
        Transition(ServiceOrderAction.MerchantCancelled, ServiceOrderStatus.MerchantCancelled, actor, null, now);
    }

    public void Deliver(string actor, DateTime now)
    {
        EnsureTransition(ServiceOrderStatus.InProgress, ServiceOrderStatus.Delivered);
        DeliveredAt = now;
        Transition(ServiceOrderAction.Delivered, ServiceOrderStatus.Delivered, actor, null, now);
    }

    public void MerchantAccept(string actor, DateTime now)
    {
        EnsureTransition(ServiceOrderStatus.Delivered, ServiceOrderStatus.Accepted);
        Transition(ServiceOrderAction.MerchantAccepted, ServiceOrderStatus.Accepted, actor, null, now);
    }

    /// <summary>From Delivered, mandatory note → back to InProgress; revisionCount++ (unlimited in v1 —
    /// listing terms are informational).</summary>
    public void RequestRevision(string actor, string note, DateTime now)
    {
        RequireNote(note);
        EnsureTransition(ServiceOrderStatus.Delivered, ServiceOrderStatus.InProgress);
        RevisionCount++;
        DeliveredAt = null;
        Transition(ServiceOrderAction.RevisionRequested, ServiceOrderStatus.InProgress, actor, note.Trim(), now);
    }

    /// <summary>Accepted → Closed — immediate in v1; kept a DISTINCT transition for the future escrow
    /// release point.</summary>
    public void Close(string actor, DateTime now)
    {
        EnsureTransition(ServiceOrderStatus.Accepted, ServiceOrderStatus.Closed);
        ClosedAt = now;
        Transition(ServiceOrderAction.Closed, ServiceOrderStatus.Closed, actor, null, now);
    }

    /// <summary>
    /// The Salla calibration: due when now ≥ deliveredAt + 7 days (time-parameterized — the
    /// IsActiveAsOf precedent; no background-job infra in this gate). Records actor = System and
    /// closes immediately. Idempotent: anything not currently Delivered-and-due is a no-op.
    /// </summary>
    public bool AutoAcceptIfDue(DateTime now)
    {
        if (Status != ServiceOrderStatus.Delivered || DeliveredAt == null)
        {
            return false;
        }

        if (now < DeliveredAt.Value + PartnerCatalogServiceOrderConsts.AutoAcceptAfter)
        {
            return false;
        }

        Transition(ServiceOrderAction.AutoAccepted, ServiceOrderStatus.Accepted,
            PartnerCatalogServiceOrderConsts.SystemActor, null, now);
        Close(PartnerCatalogServiceOrderConsts.SystemActor, now);
        return true;
    }

    // ---- guards + history ---------------------------------------------------------------------------

    private void ApplyMilestones(IReadOnlyList<ServiceOrderMilestoneInput>? inputs)
    {
        if (inputs == null || inputs.Count == 0)
        {
            return; // optional
        }

        var price = SettlementMoney.Round(MerchantPriceAmount);
        if (price < PartnerCatalogServiceOrderConsts.MilestoneMinimumOrderPrice)
        {
            throw new BusinessException(PartnerCatalogServiceOrderErrorCodes.MilestonesNotAllowedBelowMinimum)
                .WithData("Price", price)
                .WithData("Minimum", PartnerCatalogServiceOrderConsts.MilestoneMinimumOrderPrice);
        }

        if (inputs.Count is < PartnerCatalogServiceOrderConsts.MinMilestones
            or > PartnerCatalogServiceOrderConsts.MaxMilestones)
        {
            throw new BusinessException(PartnerCatalogServiceOrderErrorCodes.MilestoneRowCountInvalid)
                .WithData("Count", inputs.Count);
        }

        var rows = new List<ServiceOrderMilestone>(inputs.Count);
        foreach (var input in inputs)
        {
            var title = (input.Title ?? string.Empty).Trim();
            if (title.Length == 0 ||
                title.Length > PartnerCatalogServiceOrderConsts.MaxMilestoneTitleLength ||
                input.Amount <= 0m)
            {
                throw new BusinessException(PartnerCatalogServiceOrderErrorCodes.MilestoneRowInvalid)
                    .WithData("Title", title)
                    .WithData("Amount", input.Amount);
            }

            rows.Add(new ServiceOrderMilestone(Guid.NewGuid(), rows.Count, title, SettlementMoney.Round(input.Amount)));
        }

        // First share ≥ 40% — computed with the EXISTING money rounding, no new math.
        var firstMinimum = SettlementMoney.Round(price * PartnerCatalogServiceOrderConsts.MilestoneFirstMinimumShare);
        if (rows[0].Amount < firstMinimum)
        {
            throw new BusinessException(PartnerCatalogServiceOrderErrorCodes.MilestoneFirstShareTooSmall)
                .WithData("First", rows[0].Amount)
                .WithData("RequiredMinimum", firstMinimum);
        }

        var sum = SettlementMoney.Round(rows.Sum(r => r.Amount));
        if (sum != price)
        {
            throw new BusinessException(PartnerCatalogServiceOrderErrorCodes.MilestoneSumMismatch)
                .WithData("Sum", sum)
                .WithData("Price", price);
        }

        _milestones.AddRange(rows);
    }

    private static void RequireNote(string note)
    {
        if (string.IsNullOrWhiteSpace(note))
        {
            throw new BusinessException(PartnerCatalogServiceOrderErrorCodes.OrderNoteRequired);
        }
    }

    private void EnsureBeforeInProgress()
    {
        EnsureNotTerminal();
        if (Status is not (ServiceOrderStatus.Draft or ServiceOrderStatus.RequirementsSubmitted))
        {
            throw new BusinessException(PartnerCatalogServiceOrderErrorCodes.IllegalOrderTransition)
                .WithData("From", Status.ToString());
        }
    }

    private void EnsureTransition(ServiceOrderStatus expectedFrom, ServiceOrderStatus to)
    {
        EnsureNotTerminal();
        if (Status != expectedFrom)
        {
            throw new BusinessException(PartnerCatalogServiceOrderErrorCodes.IllegalOrderTransition)
                .WithData("From", Status.ToString())
                .WithData("To", to.ToString());
        }
    }

    private void EnsureNotTerminal()
    {
        if (IsTerminal)
        {
            throw new BusinessException(PartnerCatalogServiceOrderErrorCodes.OrderTerminalImmutable)
                .WithData("Status", Status.ToString());
        }
    }

    private void Transition(ServiceOrderAction action, ServiceOrderStatus to, string actor, string? note, DateTime at)
    {
        Append(action, Status, to, actor, note, at);
        Status = to;
    }

    private void Append(ServiceOrderAction action, ServiceOrderStatus? from, ServiceOrderStatus to, string actor, string? note, DateTime at)
    {
        _history.Add(new ServiceOrderHistoryEntry(
            Guid.NewGuid(), _history.Count, action, from, to, (actor ?? string.Empty).Trim(), note, at));
    }
}

/// <summary>Milestone write input (declared plan row).</summary>
public sealed record ServiceOrderMilestoneInput(string Title, decimal Amount);

// ---- owned rows (explicit Guid keys; app-managed OrderIndex — the 2a pattern) ---------------------

/// <summary>A typed merchant answer; the requirement title is SNAPSHOTTED at answer time.</summary>
public class ServiceOrderAnswer
{
    public Guid Id { get; private set; }
    public int OrderIndex { get; private set; }
    public string RequirementTitleSnapshot { get; private set; } = string.Empty;
    public ListingRequirementType RequirementType { get; private set; }
    public string AnswerText { get; private set; } = string.Empty;

    protected ServiceOrderAnswer()
    {
    }

    internal ServiceOrderAnswer(Guid id, int orderIndex, string requirementTitleSnapshot, ListingRequirementType type, string answerText)
    {
        Id = id;
        OrderIndex = orderIndex;
        RequirementTitleSnapshot = requirementTitleSnapshot;
        RequirementType = type;
        AnswerText = answerText;
    }
}

/// <summary>Declared payment-plan row — DATA ONLY (no execution status until the escrow gate).</summary>
public class ServiceOrderMilestone
{
    public Guid Id { get; private set; }
    public int OrderIndex { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public decimal Amount { get; private set; }

    protected ServiceOrderMilestone()
    {
    }

    internal ServiceOrderMilestone(Guid id, int orderIndex, string title, decimal amount)
    {
        Id = id;
        OrderIndex = orderIndex;
        Title = title;
        Amount = amount;
    }
}

/// <summary>Append-only status history row (actor + optional note) — never overwritten.</summary>
public class ServiceOrderHistoryEntry
{
    public Guid Id { get; private set; }
    public int OrderIndex { get; private set; }
    public ServiceOrderAction Action { get; private set; }
    public ServiceOrderStatus? FromStatus { get; private set; }
    public ServiceOrderStatus ToStatus { get; private set; }
    public string Actor { get; private set; } = string.Empty;
    public string? Note { get; private set; }
    public DateTime At { get; private set; }

    protected ServiceOrderHistoryEntry()
    {
    }

    internal ServiceOrderHistoryEntry(Guid id, int orderIndex, ServiceOrderAction action, ServiceOrderStatus? fromStatus, ServiceOrderStatus toStatus, string actor, string? note, DateTime at)
    {
        Id = id;
        OrderIndex = orderIndex;
        Action = action;
        FromStatus = fromStatus;
        ToStatus = toStatus;
        Actor = actor;
        Note = note;
        At = at;
    }
}
