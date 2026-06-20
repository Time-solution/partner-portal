using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;
using Zahy.Settlement;

namespace Zahy.PartnerCatalog.Read;

/// <summary>Money contract for portal live-swap (mirrors domain three-part money).</summary>
public class MoneyDto
{
    public decimal Amount { get; set; }
    public string Currency { get; set; } = PartnerCatalogConsts.DefaultCurrency;
    public bool VatInclusive { get; set; }
}

public class PartnerCatalogItemReadDto : EntityDto<Guid>
{
    public Guid PartnerId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public PartnerCatalogOfferingKind OfferingKind { get; set; }
    public MoneyDto PartnerCost { get; set; } = new();
    public PartnerCatalogItemStatus Status { get; set; }
    public SettlementBook? SettlementBookOverride { get; set; }
    public SettlementTriggerMode SettlementTriggerMode { get; set; }
    public VatTreatment DefaultVatTreatment { get; set; }
    public DateTime? ArchivedAt { get; set; }
    public string? CarrierServiceCode { get; set; }
    public PartnerCatalogFulfilmentUnit? FulfilmentUnit { get; set; }
    public string? ExternalMenuItemId { get; set; }
    public string? MenuCategoryCode { get; set; }
    public bool RequiresPlatformCatalogSync { get; set; }
    public SettlementParticipationMode SettlementParticipationMode { get; set; }
    public SettlementBook EffectiveSettlementBook { get; set; }
}

public class MerchantActivationReadDto : EntityDto<Guid>
{
    public Guid? TenantId { get; set; }
    public Guid PartnerId { get; set; }
    public Guid PartnerCatalogItemId { get; set; }
    public MoneyDto ResalePrice { get; set; } = new();
    public MerchantActivationStatus Status { get; set; }
    public DateTime? ActivatedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;
    public string? ExternalReference { get; set; }
}

public class SettlementCostMarkupSnapshotReadDto : EntityDto<Guid>
{
    public Guid MerchantActivationId { get; set; }
    public Guid PartnerCatalogItemId { get; set; }
    public Guid PartnerId { get; set; }
    public Guid TenantId { get; set; }
    public MoneyDto BuyPrice { get; set; } = new();
    public MoneyDto SellPrice { get; set; } = new();
    public PartnerCatalogSellPriceSource SellPriceSource { get; set; }
    public SettlementBook SettlementBook { get; set; }
    public VatTreatment VatTreatment { get; set; }
    public SettlementCostMarkupTrigger Trigger { get; set; }
    public string ExternalTransactionId { get; set; } = string.Empty;
    public string OrderLineId { get; set; } = string.Empty;
    public Guid? SettlementCaseId { get; set; }
    public Guid? BillingChargeId { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class ReflectedPartnerOrderReadDto : EntityDto<Guid>
{
    public Guid PartnerId { get; set; }
    public Guid TenantId { get; set; }
    public Guid MerchantActivationId { get; set; }
    public Guid PartnerCatalogItemId { get; set; }
    public Guid SettlementCostMarkupSnapshotId { get; set; }
    public string ExternalTransactionId { get; set; } = string.Empty;
    public string OrderLineId { get; set; } = string.Empty;
    public DateTime ReflectedAt { get; set; }
}

public class PlatformCatalogLinkReadDto : EntityDto<Guid>
{
    public Guid PartnerCatalogItemId { get; set; }
    public Guid? TenantId { get; set; }
    public PlatformCatalogLinkStatus Status { get; set; }
    public Guid? PlatformProductId { get; set; }
    public Guid? PlatformVariantId { get; set; }
    public DateTime? LastSyncAttemptAt { get; set; }
    public string? LastSyncError { get; set; }
    public string? Shape2HandshakeVersion { get; set; }
}

public class PartnerCatalogItemsQuery
{
    public Guid? PartnerId { get; set; }
}

public class MerchantActivationsQuery
{
    public Guid? PartnerId { get; set; }
    public Guid? TenantId { get; set; }
}

public class PartnerCatalogPartnerQuery
{
    public Guid? PartnerId { get; set; }
}

public class PlatformCatalogLinksQuery
{
    public Guid? PartnerId { get; set; }
    public Guid? PartnerCatalogItemId { get; set; }
}
