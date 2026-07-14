using Zahy.PartnerCatalog.Read;
using Zahy.Settlement;

namespace Zahy.PartnerCatalog.Merchant;

/// <summary>
/// Scoped offering visibility — the SINGLE source for the merchant-facing offering projection
/// (mirrors <see cref="Packages.UsagePackageVisibility"/>). The merchant sees ONE money field:
/// <c>Price</c> = the resolved SELL — the open activation's ResalePrice when one exists, else the
/// default sell (the same ResolveResalePrice fallback the activation and service-order flows charge).
/// The BUY leg (PartnerCost) is STRUCTURALLY ABSENT from the merchant DTO — the property does not
/// exist on the type, not hidden/null. Partner/admin item reads stay on the central
/// <see cref="PartnerCatalogReadDtoMapper"/> (buy for partner; buy+sell snapshots for admin);
/// items carry no margin field for any audience (unchanged). PURE — no money math.
/// </summary>
public static class PartnerCatalogOfferingVisibility
{
    public static MerchantPartnerOfferingReadDto ToMerchantDto(
        PartnerCatalogItem item,
        string? partnerBrief,
        Money resolvedPrice) =>
        new()
        {
            Id = item.Id,
            PartnerId = item.PartnerId,
            Code = item.Code,
            Name = item.Name,
            // Description doubles as the merchant-facing OfferingSummary (reused, not duplicated).
            Description = item.Description,
            PartnerBrief = partnerBrief,
            MerchantBenefit = item.MerchantBenefit,
            OfferingKind = item.OfferingKind,
            Price = PartnerCatalogReadDtoMapper.ToMoneyDto(resolvedPrice),
            SettlementParticipationMode = item.SettlementParticipationMode,
            SettlementTriggerMode = item.SettlementTriggerMode,
        };
}
