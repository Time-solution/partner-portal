using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;

namespace Zahy.PartnerCatalog;

/// <summary>
/// U4 — a MERCHANT's selection of a partner's PUBLISHED usage package (instant self-service activation,
/// no approval). Records WHICH package a merchant is on for a partner, plus the active window
/// (<see cref="ActivatedAt"/>..<see cref="EndedAt"/>) so U3 can prorate the base by calendar days and
/// count usage to the deactivation moment. SELECTION/LINK ONLY — holding this row posts NO journal and
/// computes NO billing (that is the gated U3 calc, which reads this for the active window).
/// </summary>
public class UsagePackageSelection : FullAuditedAggregateRoot<Guid>
{
    public Guid PartnerId { get; private set; }

    /// <summary>The merchant tenant that selected the package.</summary>
    public Guid TenantId { get; private set; }

    public Guid UsagePackageId { get; private set; }

    public string MerchantName { get; private set; } = string.Empty;

    public UsagePackageSelectionStatus Status { get; private set; } = UsagePackageSelectionStatus.Active;

    /// <summary>When the merchant selected the package — start of the active window (for proration).</summary>
    public DateTime ActivatedAt { get; private set; }

    /// <summary>When deactivated mid-cycle (null while Active) — end of the active window.</summary>
    public DateTime? EndedAt { get; private set; }

    protected UsagePackageSelection()
    {
    }

    public UsagePackageSelection(
        Guid id,
        Guid partnerId,
        Guid tenantId,
        Guid usagePackageId,
        string merchantName,
        DateTime activatedAt)
        : base(id)
    {
        if (partnerId == Guid.Empty || tenantId == Guid.Empty || usagePackageId == Guid.Empty)
        {
            throw new BusinessException(PartnerCatalogErrorCodes.InvalidUsagePackageSelection)
                .WithData("Reason", "IdsRequired");
        }

        PartnerId = partnerId;
        TenantId = tenantId;
        UsagePackageId = usagePackageId;
        MerchantName = (merchantName ?? string.Empty).Trim();
        ActivatedAt = activatedAt;
        Status = UsagePackageSelectionStatus.Active; // instant — no approval step
    }

    /// <summary>Mid-cycle deactivation. Append-only state change; the active window closes at <paramref name="endedAt"/>.</summary>
    public void End(DateTime endedAt)
    {
        if (Status == UsagePackageSelectionStatus.Ended)
        {
            throw new BusinessException(PartnerCatalogErrorCodes.UsagePackageSelectionAlreadyEnded)
                .WithData("UsagePackageSelectionId", Id);
        }

        Status = UsagePackageSelectionStatus.Ended;
        EndedAt = endedAt;
    }
}
