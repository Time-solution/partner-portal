namespace Zahy.PartnerCatalog;

public enum PartnerCatalogParticipationOutcome
{
    PrincipalDispatched = 1,
    PrincipalDuplicate = 2,
    PrincipalReversed = 3,
    ReflectionRecorded = 4,
    ReflectionDuplicate = 5,
    SubscriptionCharged = 6,
    SubscriptionDuplicate = 7,
    Skipped = 8
}

public sealed class PartnerCatalogParticipationResult
{
    public PartnerCatalogParticipationOutcome Outcome { get; init; }

    public Guid? SettlementCaseId { get; init; }

    public Guid? BillingChargeId { get; init; }

    public Guid? ReflectedPartnerOrderId { get; init; }

    public string? SkipReason { get; init; }

    public decimal OutputVat { get; init; }

    public decimal InputVat { get; init; }

    public decimal Margin { get; init; }

    public decimal NetVatToZatca { get; init; }

    public decimal TotalDebits { get; init; }

    public decimal TotalCredits { get; init; }
}

/// <summary>
/// Routes a snapshot by <see cref="SettlementParticipationMode"/> — Principal (2b), ReflectionOnly, SubscriptionFee (2c).
/// </summary>
public interface IPartnerCatalogParticipationBridge
{
    Task<PartnerCatalogParticipationResult> DispatchAsync(
        SettlementCostMarkupSnapshot snapshot,
        PartnerCatalogItem catalogItem,
        MerchantActivation activation,
        string? billingPeriodKey = null,
        CancellationToken cancellationToken = default);

    Task<PartnerCatalogParticipationResult> ReversePrincipalAsync(
        SettlementCostMarkupSnapshot originalSnapshot,
        PartnerCatalogItem catalogItem,
        CancellationToken cancellationToken = default);
}
