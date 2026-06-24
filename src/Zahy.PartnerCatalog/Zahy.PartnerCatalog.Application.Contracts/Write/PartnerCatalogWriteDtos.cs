using System;
using Zahy.PartnerCatalog.Read;
using Zahy.Settlement;

namespace Zahy.PartnerCatalog.Write;

public class CreatePartnerCatalogItemInput
{
    public Guid PartnerId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    /// <summary>Phase 6a — "what the merchant gets / why activate this." Optional plain text (≤400).</summary>
    public string? MerchantBenefit { get; set; }

    public PartnerCatalogOfferingKind OfferingKind { get; set; }
    public MoneyDto PartnerCost { get; set; } = new();
    public SettlementBook? SettlementBookOverride { get; set; }
    public SettlementTriggerMode? SettlementTriggerMode { get; set; }
    public string? CarrierServiceCode { get; set; }
    public PartnerCatalogFulfilmentUnit? FulfilmentUnit { get; set; }
    public string? ExternalMenuItemId { get; set; }
    public string? MenuCategoryCode { get; set; }
    public SettlementParticipationMode SettlementParticipationMode { get; set; } =
        SettlementParticipationMode.Principal;
    public ConsignmentOwnershipMode? ConsignmentOwnershipMode { get; set; }
}

public class UpdatePartnerCatalogItemInput
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    /// <summary>Phase 6a — "what the merchant gets / why activate this." Optional plain text (≤400).</summary>
    public string? MerchantBenefit { get; set; }

    public MoneyDto PartnerCost { get; set; } = new();
    public SettlementBook? SettlementBookOverride { get; set; }
    public SettlementTriggerMode? SettlementTriggerMode { get; set; }
    public string? CarrierServiceCode { get; set; }
    public PartnerCatalogFulfilmentUnit? FulfilmentUnit { get; set; }
    public string? ExternalMenuItemId { get; set; }
    public string? MenuCategoryCode { get; set; }
    public SettlementParticipationMode? SettlementParticipationMode { get; set; }
    public ConsignmentOwnershipMode? ConsignmentOwnershipMode { get; set; }
}
