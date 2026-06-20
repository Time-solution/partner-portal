using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp.AspNetCore.Mvc;
using Zahy.PartnerCatalog.Read;

namespace Zahy.PartnerCatalog.Merchant;

/// <summary>
/// Merchant self-service partner activation API (not wired to the React portal yet).
/// </summary>
[Route("api/partner-catalog/merchant")]
public class PartnerCatalogMerchantController : AbpControllerBase
{
    private readonly IPartnerCatalogMerchantAppService _merchantAppService;

    public PartnerCatalogMerchantController(IPartnerCatalogMerchantAppService merchantAppService)
    {
        _merchantAppService = merchantAppService;
    }

    [HttpGet("offerings")]
    public Task<List<MerchantPartnerOfferingReadDto>> GetAvailableOfferingsAsync() =>
        _merchantAppService.GetAvailableOfferingsAsync();

    [HttpGet("activations")]
    public Task<List<MerchantActivationReadDto>> GetMyActivationsAsync([FromQuery] Guid? tenantId) =>
        _merchantAppService.GetMyActivationsAsync(new MerchantActivationsForTenantQuery
        {
            TenantId = tenantId,
        });

    [HttpPost("activations")]
    public Task<MerchantActivationReadDto> ActivateAsync([FromBody] ActivateMerchantOfferingInput input) =>
        _merchantAppService.ActivateAsync(input);

    [HttpPost("activations/{activationId:guid}/deactivate")]
    public Task<MerchantActivationReadDto> DeactivateAsync(Guid activationId) =>
        _merchantAppService.DeactivateAsync(activationId);
}
