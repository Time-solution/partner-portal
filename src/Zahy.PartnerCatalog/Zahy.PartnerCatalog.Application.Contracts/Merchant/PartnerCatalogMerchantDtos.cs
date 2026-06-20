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
    public string? Description { get; set; }
    public PartnerCatalogOfferingKind OfferingKind { get; set; }
    public MoneyDto PartnerCost { get; set; } = new();
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
