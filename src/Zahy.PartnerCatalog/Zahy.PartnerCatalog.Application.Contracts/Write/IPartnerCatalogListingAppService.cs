using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace Zahy.PartnerCatalog.Write;

/// <summary>
/// Gate 2a — structured listing authoring (presentation-only, NOT draft-gated; the ratified 6b
/// convention). Authoring rights reuse the EXISTING PartnerCatalogWriteAccessGuard: service
/// partners self-author their own offerings, delivery/3PL are admin-managed, admin authors any.
/// Config + display only — no order lifecycle, no billing, no posting.
/// </summary>
public interface IPartnerCatalogListingAppService : IApplicationService
{
    /// <summary>Whole-document replace; empty sections clear. Domain owns every cap/invariant.</summary>
    Task<PartnerCatalogListingDto> UpdateListingAsync(Guid partnerCatalogItemId, UpdatePartnerCatalogListingInput input);

    /// <summary>The offering's listing (empty DTO when none authored yet). Same read scoping as items.</summary>
    Task<PartnerCatalogListingDto> GetListingAsync(Guid partnerCatalogItemId);
}
