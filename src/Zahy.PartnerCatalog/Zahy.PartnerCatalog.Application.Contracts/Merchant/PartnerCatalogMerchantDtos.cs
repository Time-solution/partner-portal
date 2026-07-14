using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;
using Zahy.PartnerCatalog.Read;
using Zahy.Settlement;

namespace Zahy.PartnerCatalog.Merchant;

/// <summary>Partner offering visible to merchants for self-activation (browse).</summary>
public class MerchantPartnerOfferingReadDto : EntityDto<Guid>
{
    public Guid PartnerId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;

    /// <summary>Phase 6a — human description of the service; this IS the merchant-facing OfferingSummary.</summary>
    public string? Description { get; set; }

    /// <summary>Phase 6a — partner self-introduction (partner-level), read-only for merchants.</summary>
    public string? PartnerBrief { get; set; }

    /// <summary>Phase 6a — "what the merchant gets / why activate this" (offering-level), read-only.</summary>
    public string? MerchantBenefit { get; set; }

    public PartnerCatalogOfferingKind OfferingKind { get; set; }

    /// <summary>
    /// The RESOLVED sell — what an activation will charge this merchant: the open activation's
    /// ResalePrice when one exists, else the default sell (ResolveResalePrice's fallback). The BUY
    /// leg (PartnerCost) is STRUCTURALLY ABSENT from this DTO — merchant audience never sees it.
    /// </summary>
    public MoneyDto Price { get; set; } = new();

    public SettlementParticipationMode SettlementParticipationMode { get; set; }
    public SettlementTriggerMode SettlementTriggerMode { get; set; }
}

public class ActivateMerchantOfferingInput
{
    public Guid PartnerCatalogItemId { get; set; }

    /// <summary>Optional resale price; defaults to catalog partner cost when omitted.</summary>
    public MoneyDto? ResalePrice { get; set; }

    public string? ExternalReference { get; set; }
}

public class MerchantActivationsForTenantQuery
{
    /// <summary>When omitted, uses the current merchant tenant context.</summary>
    public Guid? TenantId { get; set; }
}
