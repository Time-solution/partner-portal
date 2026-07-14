using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;
using Zahy.Settlement;

namespace Zahy.PartnerCatalog;

/// <summary>
/// Merchant commits to resell a partner catalog item. Settlement trigger mode is read from the parent
/// <see cref="PartnerCatalogItem"/> (Q2) — not stored on this aggregate.
/// </summary>
public class MerchantActivation : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    /// <summary>Merchant tenant id (merchant scope / logical merchant id).</summary>
    public Guid? TenantId { get; private set; }

    /// <summary>Denormalized from the catalog item at construction; immutable thereafter.</summary>
    public Guid PartnerId { get; private set; }

    public Guid PartnerCatalogItemId { get; private set; }

    public decimal ResalePriceAmount { get; private set; }

    public string ResalePriceCurrency { get; private set; } = PartnerCatalogConsts.DefaultCurrency;

    public bool ResalePriceVatInclusive { get; private set; }

    public MerchantActivationStatus Status { get; private set; }

    public DateTime? ActivatedAt { get; private set; }

    public DateTime? EndedAt { get; private set; }

    public string IdempotencyKey { get; private set; } = string.Empty;

    public string? ExternalReference { get; private set; }

    public Money ResalePrice =>
        PartnerCatalogMoneyAssignment.Read(ResalePriceAmount, ResalePriceCurrency, ResalePriceVatInclusive);

    protected MerchantActivation()
    {
    }

    public static MerchantActivation Create(
        Guid id,
        Guid tenantId,
        PartnerCatalogItem catalogItem,
        Money resalePrice,
        string? externalReference = null,
        int priorEndedCount = 0)
    {
        if (tenantId == Guid.Empty)
        {
            throw new BusinessException(PartnerCatalogErrorCodes.InvalidActivation)
                .WithData("Reason", "TenantIdRequired");
        }

        if (priorEndedCount < 0)
        {
            throw new BusinessException(PartnerCatalogErrorCodes.InvalidActivation)
                .WithData("Reason", "NegativePriorEndedCount");
        }

        Check.NotNull(catalogItem, nameof(catalogItem));

        if (catalogItem.Status != PartnerCatalogItemStatus.Active)
        {
            throw new BusinessException(PartnerCatalogErrorCodes.ItemNotActive)
                .WithData("PartnerCatalogItemId", catalogItem.Id);
        }

        var activation = new MerchantActivation
        {
            Id = id,
            TenantId = tenantId,
            PartnerId = catalogItem.PartnerId,
            PartnerCatalogItemId = catalogItem.Id,
            Status = MerchantActivationStatus.Pending,
            IdempotencyKey = BuildIdempotencyKey(tenantId, catalogItem.Id, priorEndedCount),
            ExternalReference = NormalizeOptional(externalReference, PartnerCatalogConsts.MaxExternalReferenceLength)
        };

        activation.AssignResalePrice(resalePrice);

        return activation;
    }

    /// <summary>Settlement trigger is owned by the catalog item (DESIGN §3.1 / Q2).</summary>
    public SettlementTriggerMode ResolveSettlementTriggerMode(PartnerCatalogItem catalogItem)
    {
        EnsureCatalogItemMatches(catalogItem);
        return catalogItem.SettlementTriggerMode;
    }

    public void Activate(DateTime atUtc) => TransitionTo(MerchantActivationStatus.Active, atUtc, setActivatedAt: true);

    public void Suspend(DateTime atUtc) => TransitionTo(MerchantActivationStatus.Suspended, atUtc);

    public void Resume(DateTime atUtc) => TransitionTo(MerchantActivationStatus.Active, atUtc, setActivatedAt: true);

    public void End(DateTime atUtc) => TransitionTo(MerchantActivationStatus.Ended, atUtc, setEndedAt: true);

    public void Cancel(DateTime atUtc) => TransitionTo(MerchantActivationStatus.Ended, atUtc, setEndedAt: true);

    /// <summary>
    /// Sequence-suffixed key (re-activation ratified ALLOWED): each ENDED cycle for the same
    /// (tenant, item) pair increments the suffix, so a re-activation is a NEW row under the same
    /// plain unique index — no filtered index needed, provider-safe. Concurrent duplicate submits
    /// compute the same suffix and collide on the index; the loser resolves to the open-activation
    /// pre-check no-op on retry. Ended rows are never mutated.
    /// </summary>
    public static string BuildIdempotencyKey(Guid tenantId, Guid partnerCatalogItemId, int priorEndedCount) =>
        $"activation:{tenantId:D}:{partnerCatalogItemId:D}:{priorEndedCount}";

    private void TransitionTo(
        MerchantActivationStatus target,
        DateTime atUtc,
        bool setActivatedAt = false,
        bool setEndedAt = false)
    {
        if (!MerchantActivationStateMachine.CanTransition(Status, target))
        {
            throw new BusinessException(PartnerCatalogErrorCodes.InvalidStatusTransition)
                .WithData("From", Status.ToString())
                .WithData("To", target.ToString());
        }

        Status = target;

        if (setActivatedAt)
        {
            ActivatedAt = atUtc;
        }

        if (setEndedAt)
        {
            EndedAt = atUtc;
        }
    }

    private void AssignResalePrice(Money resalePrice)
    {
        PartnerCatalogMoneyAssignment.Assign(
            resalePrice,
            amount => ResalePriceAmount = amount,
            currency => ResalePriceCurrency = currency,
            vatInclusive => ResalePriceVatInclusive = vatInclusive,
            PartnerCatalogErrorCodes.InvalidResalePrice);
    }

    private void EnsureCatalogItemMatches(PartnerCatalogItem catalogItem)
    {
        if (catalogItem.Id != PartnerCatalogItemId || catalogItem.PartnerId != PartnerId)
        {
            throw new BusinessException(PartnerCatalogErrorCodes.CatalogItemMismatch)
                .WithData("PartnerCatalogItemId", PartnerCatalogItemId);
        }
    }

    private static string? NormalizeOptional(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        if (trimmed.Length > maxLength)
        {
            throw new BusinessException(PartnerCatalogErrorCodes.InvalidActivation)
                .WithData("MaxLength", maxLength);
        }

        return trimmed;
    }
}
