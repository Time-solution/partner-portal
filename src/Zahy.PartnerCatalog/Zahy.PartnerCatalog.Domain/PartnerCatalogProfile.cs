using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;

namespace Zahy.PartnerCatalog;

/// <summary>
/// Phase 6a — a partner's catalog-side presentation profile (ONE row per partner). Holds the partner's
/// self-introduction (<see cref="PartnerBrief"/>) surfaced to merchants. This is self-description, NOT
/// pricing: it is editable by the partner for ALL partner types (or by an admin), independent of the
/// offering authoring-by-type rule. PRESENTATION ONLY — no money, settlement, or posting input.
/// </summary>
public class PartnerCatalogProfile : FullAuditedAggregateRoot<Guid>
{
    public Guid PartnerId { get; private set; }

    /// <summary>Partner self-introduction shown to merchants. Optional plain text, length-capped.</summary>
    public string? PartnerBrief { get; private set; }

    protected PartnerCatalogProfile()
    {
    }

    public static PartnerCatalogProfile Create(Guid id, Guid partnerId, string? partnerBrief = null)
    {
        if (partnerId == Guid.Empty)
        {
            throw new BusinessException(PartnerCatalogErrorCodes.InvalidPartnerCatalogProfile)
                .WithData("Reason", "PartnerIdRequired");
        }

        var profile = new PartnerCatalogProfile
        {
            Id = id,
            PartnerId = partnerId,
        };
        profile.SetBrief(partnerBrief);
        return profile;
    }

    /// <summary>Set/clear the partner brief (null/blank clears). Plain text, trimmed, length-capped.</summary>
    public void SetBrief(string? partnerBrief)
    {
        if (string.IsNullOrWhiteSpace(partnerBrief))
        {
            PartnerBrief = null;
            return;
        }

        var trimmed = partnerBrief.Trim();
        if (trimmed.Length > PartnerCatalogConsts.MaxPartnerBriefLength)
        {
            throw new BusinessException(PartnerCatalogErrorCodes.InvalidPartnerCatalogProfile)
                .WithData("Reason", "PartnerBriefTooLong")
                .WithData("MaxLength", PartnerCatalogConsts.MaxPartnerBriefLength);
        }

        PartnerCatalogContentPolicy.EnsureNoContactChannel(trimmed, "PartnerBrief");
        PartnerBrief = trimmed;
    }
}
