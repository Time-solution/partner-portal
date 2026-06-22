using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace Zahy.PartnerCatalog.Packages;

/// <summary>
/// U4 — merchant package browse + selection. A merchant browses a partner's PUBLISHED packages (priced on
/// the SELL/fee side they pay — buy/margin structurally absent), then selects one for INSTANT self-service
/// activation. SELECTION/LINK ONLY — no billing computed, no journal posted (U3 reads the active window).
/// </summary>
public interface IUsagePackageSelectionAppService : IApplicationService
{
    /// <summary>A partner's published packages, scoped to the MERCHANT view (sell/fee only).</summary>
    Task<List<UsagePackageDto>> GetPublishedAsync(Guid partnerId);

    /// <summary>Instantly activate (select) a published package for the merchant. No approval step.</summary>
    Task<UsagePackageSelectionDto> SelectAsync(SelectUsagePackageInput input);

    /// <summary>Deactivate a selection mid-cycle (base prorated by days, usage counted to deactivation in U3).</summary>
    Task<UsagePackageSelectionDto> EndAsync(Guid id);

    /// <summary>The merchant's selections (which package they're on, per partner).</summary>
    Task<List<UsagePackageSelectionDto>> GetForMerchantAsync(Guid tenantId);
}
