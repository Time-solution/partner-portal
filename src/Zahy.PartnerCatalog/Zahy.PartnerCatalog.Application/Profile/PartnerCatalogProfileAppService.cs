using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace Zahy.PartnerCatalog.Profile;

/// <summary>
/// Phase 6a — partner catalog-side presentation profile authoring (PartnerBrief). Self-editable by the
/// partner for ALL partner types on their own profile, or by an admin for any partner. PRESENTATION ONLY:
/// no money, settlement, or posting effect.
/// </summary>
[Authorize]
public class PartnerCatalogProfileAppService : ApplicationService, IPartnerCatalogProfileAppService
{
    private readonly IRepository<PartnerCatalogProfile, Guid> _repository;
    private readonly PartnerCatalogProfileAccessGuard _accessGuard;

    public PartnerCatalogProfileAppService(
        IRepository<PartnerCatalogProfile, Guid> repository,
        PartnerCatalogProfileAccessGuard accessGuard)
    {
        _repository = repository;
        _accessGuard = accessGuard;
    }

    public async Task<PartnerCatalogProfileDto?> GetAsync(Guid partnerId)
    {
        var profile = await _repository.FirstOrDefaultAsync(x => x.PartnerId == partnerId);
        return profile == null ? null : ToDto(profile);
    }

    public async Task<PartnerCatalogProfileDto> SetBriefAsync(SetPartnerBriefInput input)
    {
        Check.NotNull(input, nameof(input));
        await _accessGuard.EnsureCanEditBriefAsync(input.PartnerId);

        var profile = await _repository.FirstOrDefaultAsync(x => x.PartnerId == input.PartnerId);
        if (profile == null)
        {
            profile = PartnerCatalogProfile.Create(GuidGenerator.Create(), input.PartnerId, input.PartnerBrief);
            await _repository.InsertAsync(profile, autoSave: true);
        }
        else
        {
            profile.SetBrief(input.PartnerBrief);
            await _repository.UpdateAsync(profile, autoSave: true);
        }

        return ToDto(profile);
    }

    private static PartnerCatalogProfileDto ToDto(PartnerCatalogProfile profile) =>
        new()
        {
            Id = profile.Id,
            PartnerId = profile.PartnerId,
            PartnerBrief = profile.PartnerBrief,
        };
}
