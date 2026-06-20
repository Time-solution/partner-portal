using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;
using Zahy.Settlement;

namespace Zahy.PartnerCatalog;

/// <summary>
/// Host-level partner offering (cross-tenant). Isolated by <see cref="PartnerId"/>.
/// VAT: Principal only — feeds settlement bridge in 2b/2c; no Agent treatment.
/// </summary>
public class PartnerCatalogItem : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; protected set; }

    public Guid PartnerId { get; private set; }

    public string Code { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public PartnerCatalogOfferingKind OfferingKind { get; private set; }

    public decimal PartnerCostAmount { get; private set; }

    public string PartnerCostCurrency { get; private set; } = PartnerCatalogConsts.DefaultCurrency;

    public bool PartnerCostVatInclusive { get; private set; }

    public PartnerCatalogItemStatus Status { get; private set; }

    public SettlementBook? SettlementBookOverride { get; private set; }

    public SettlementTriggerMode SettlementTriggerMode { get; private set; }

    /// <summary>Accountant confirmed Principal for all partner types; Agent override not permitted.</summary>
    public VatTreatment DefaultVatTreatment { get; private set; }

    public DateTime? ArchivedAt { get; private set; }

    public string? CarrierServiceCode { get; private set; }

    public PartnerCatalogFulfilmentUnit? FulfilmentUnit { get; private set; }

    public string? ExternalMenuItemId { get; private set; }

    public string? MenuCategoryCode { get; private set; }

    public bool RequiresPlatformCatalogSync { get; private set; }

    /// <summary>Settlement participation routing — stored only; bridge uses in 2b/2c.</summary>
    public SettlementParticipationMode SettlementParticipationMode { get; private set; }

    public Money PartnerCost =>
        PartnerCatalogMoneyAssignment.Read(PartnerCostAmount, PartnerCostCurrency, PartnerCostVatInclusive);

    public SettlementBook EffectiveSettlementBook =>
        SettlementBookOverride ?? PartnerCatalogOfferingKindDefaults.GetDefaultSettlementBook(OfferingKind);

    protected PartnerCatalogItem()
    {
    }

    public static PartnerCatalogItem Create(
        Guid id,
        Guid partnerId,
        string code,
        string name,
        string? description,
        PartnerCatalogOfferingKind offeringKind,
        Money partnerCost,
        SettlementBook? settlementBookOverride = null,
        SettlementTriggerMode? settlementTriggerMode = null,
        string? carrierServiceCode = null,
        PartnerCatalogFulfilmentUnit? fulfilmentUnit = null,
        string? externalMenuItemId = null,
        string? menuCategoryCode = null,
        SettlementParticipationMode settlementParticipationMode = SettlementParticipationMode.Principal)
    {
        if (partnerId == Guid.Empty)
        {
            throw new BusinessException(PartnerCatalogErrorCodes.InvalidOfferingKind)
                .WithData("Reason", "PartnerIdRequired");
        }

        var item = new PartnerCatalogItem
        {
            Id = id,
            TenantId = null,
            PartnerId = partnerId,
            Code = NormalizeRequired(code, PartnerCatalogConsts.MaxCodeLength, nameof(code)),
            Name = NormalizeRequired(name, PartnerCatalogConsts.MaxNameLength, nameof(name)),
            Description = NormalizeOptional(description, PartnerCatalogConsts.MaxDescriptionLength),
            OfferingKind = offeringKind,
            Status = PartnerCatalogItemStatus.Draft,
            SettlementBookOverride = settlementBookOverride,
            SettlementTriggerMode = settlementTriggerMode
                ?? PartnerCatalogOfferingKindDefaults.GetDefaultTriggerMode(offeringKind),
            DefaultVatTreatment = VatTreatment.Principal,
            SettlementParticipationMode = settlementParticipationMode,
            RequiresPlatformCatalogSync = offeringKind == PartnerCatalogOfferingKind.FnBItemsPerSale,
            CarrierServiceCode = NormalizeOptional(carrierServiceCode, PartnerCatalogConsts.MaxCarrierServiceCodeLength),
            FulfilmentUnit = offeringKind == PartnerCatalogOfferingKind.DeliveryFulfilmentPerOrder
                ? fulfilmentUnit ?? PartnerCatalogFulfilmentUnit.PerShipment
                : null,
            ExternalMenuItemId = NormalizeOptional(externalMenuItemId, PartnerCatalogConsts.MaxExternalMenuItemIdLength),
            MenuCategoryCode = NormalizeOptional(menuCategoryCode, PartnerCatalogConsts.MaxMenuCategoryCodeLength)
        };

        item.AssignPartnerCost(partnerCost);
        item.ValidateKindSpecificFields();

        return item;
    }

    public void Publish(DateTime atUtc)
    {
        TransitionTo(PartnerCatalogItemStatus.Active, atUtc);
    }

    public void Archive(DateTime atUtc)
    {
        TransitionTo(PartnerCatalogItemStatus.Archived, atUtc);
        ArchivedAt = atUtc;
    }

    public void Discard(DateTime atUtc)
    {
        if (Status != PartnerCatalogItemStatus.Draft)
        {
            throw new BusinessException(PartnerCatalogErrorCodes.InvalidStatusTransition)
                .WithData("From", Status.ToString())
                .WithData("To", PartnerCatalogItemStatus.Archived.ToString());
        }

        Archive(atUtc);
    }

    private void TransitionTo(PartnerCatalogItemStatus target, DateTime atUtc)
    {
        if (!PartnerCatalogItemStateMachine.CanTransition(Status, target))
        {
            throw new BusinessException(PartnerCatalogErrorCodes.InvalidStatusTransition)
                .WithData("From", Status.ToString())
                .WithData("To", target.ToString());
        }

        Status = target;

        if (target != PartnerCatalogItemStatus.Archived)
        {
            ArchivedAt = null;
        }
    }

    private void AssignPartnerCost(Money partnerCost)
    {
        PartnerCatalogMoneyAssignment.Assign(
            partnerCost,
            amount => PartnerCostAmount = amount,
            currency => PartnerCostCurrency = currency,
            vatInclusive => PartnerCostVatInclusive = vatInclusive);
    }

    private void ValidateKindSpecificFields()
    {
        switch (OfferingKind)
        {
            case PartnerCatalogOfferingKind.DeliveryFulfilmentPerOrder:
                if (FulfilmentUnit == null)
                {
                    throw new BusinessException(PartnerCatalogErrorCodes.InvalidOfferingKind)
                        .WithData("Reason", "DeliveryRequiresFulfilmentUnit");
                }

                break;

            case PartnerCatalogOfferingKind.FnBItemsPerSale:
                if (!RequiresPlatformCatalogSync)
                {
                    throw new BusinessException(PartnerCatalogErrorCodes.InvalidOfferingKind)
                        .WithData("Reason", "FnBRequiresPlatformCatalogSyncFlag");
                }

                break;
        }
    }

    private static string NormalizeRequired(string value, int maxLength, string paramName)
    {
        var trimmed = Check.NotNullOrWhiteSpace(value, paramName).Trim();
        if (trimmed.Length > maxLength)
        {
            throw new BusinessException(PartnerCatalogErrorCodes.InvalidOfferingKind)
                .WithData("Field", paramName)
                .WithData("MaxLength", maxLength);
        }

        return trimmed;
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
            throw new BusinessException(PartnerCatalogErrorCodes.InvalidOfferingKind)
                .WithData("MaxLength", maxLength);
        }

        return trimmed;
    }
}
