using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace Zahy.PartnerCatalog.Profile;

/// <summary>
/// Phase 6a — partner catalog-side presentation profile authoring. The partner self-edits their OWN brief
/// for ALL partner types (self-description, not pricing); an admin (manages-all) may set any partner's.
/// PRESENTATION ONLY — no money/settlement effect.
/// </summary>
public interface IPartnerCatalogProfileAppService : IApplicationService
{
    Task<PartnerCatalogProfileDto?> GetAsync(Guid partnerId);

    Task<PartnerCatalogProfileDto> SetBriefAsync(SetPartnerBriefInput input);
}
