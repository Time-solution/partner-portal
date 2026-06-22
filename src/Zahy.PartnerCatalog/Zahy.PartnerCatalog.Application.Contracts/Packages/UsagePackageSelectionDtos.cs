using System;
using Volo.Abp.Application.Dtos;

namespace Zahy.PartnerCatalog.Packages;

/// <summary>
/// U4 read DTO for a merchant's package selection (which package the merchant is on for a partner, plus
/// the active window). SELECTION/LINK ONLY — no money fields here; the priced breakdown comes from U3.
/// </summary>
public class UsagePackageSelectionDto : EntityDto<Guid>
{
    public Guid PartnerId { get; set; }
    public Guid TenantId { get; set; }
    public Guid UsagePackageId { get; set; }
    public string MerchantName { get; set; } = string.Empty;
    public UsagePackageSelectionStatus Status { get; set; }
    public DateTime ActivatedAt { get; set; }
    public DateTime? EndedAt { get; set; }
}

/// <summary>Input for a merchant selecting (instantly activating) a published usage package.</summary>
public class SelectUsagePackageInput
{
    public Guid PartnerId { get; set; }
    public Guid UsagePackageId { get; set; }
    public Guid TenantId { get; set; }
    public string MerchantName { get; set; } = string.Empty;
}
