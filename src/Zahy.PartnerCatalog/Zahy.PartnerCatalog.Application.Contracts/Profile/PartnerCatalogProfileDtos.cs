using System;
using Volo.Abp.Application.Dtos;

namespace Zahy.PartnerCatalog.Profile;

/// <summary>Phase 6a — a partner's catalog-side presentation profile (PartnerBrief).</summary>
public class PartnerCatalogProfileDto : EntityDto<Guid>
{
    public Guid PartnerId { get; set; }

    /// <summary>Partner self-introduction shown to merchants (plain text, ≤600). Null when unset.</summary>
    public string? PartnerBrief { get; set; }
}

/// <summary>Author input to set/clear a partner's brief. Editable by the partner (any type) or an admin.</summary>
public class SetPartnerBriefInput
{
    public Guid PartnerId { get; set; }

    public string? PartnerBrief { get; set; }
}
