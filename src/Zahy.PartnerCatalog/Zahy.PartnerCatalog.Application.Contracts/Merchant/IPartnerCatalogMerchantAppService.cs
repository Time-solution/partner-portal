using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;
using Zahy.PartnerCatalog.Read;

namespace Zahy.PartnerCatalog.Merchant;

/// <summary>
/// Merchant self-service partner catalog writes (activate/deactivate) scoped to the current tenant.
/// Not wired to the React portal yet — API-only for CTO review.
/// </summary>
public interface IPartnerCatalogMerchantAppService : IApplicationService
{
    Task<List<MerchantPartnerOfferingReadDto>> GetAvailableOfferingsAsync();

    Task<List<MerchantActivationReadDto>> GetMyActivationsAsync(MerchantActivationsForTenantQuery query);

    Task<MerchantActivationReadDto> ActivateAsync(ActivateMerchantOfferingInput input);

    Task<MerchantActivationReadDto> DeactivateAsync(Guid activationId);
}
