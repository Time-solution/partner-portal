using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp.AspNetCore.Mvc;

namespace Zahy.PartnerCatalog.Read;

[Route("api/partner-catalog")]
public class PartnerCatalogReadController : AbpControllerBase
{
    private readonly IPartnerCatalogReadAppService _readAppService;

    public PartnerCatalogReadController(IPartnerCatalogReadAppService readAppService)
    {
        _readAppService = readAppService;
    }

    [HttpGet("items")]
    public Task<List<PartnerCatalogItemReadDto>> GetCatalogItemsAsync([FromQuery] Guid? partnerId) =>
        _readAppService.GetCatalogItemsAsync(new PartnerCatalogItemsQuery { PartnerId = partnerId });

    [HttpGet("activations")]
    public Task<List<MerchantActivationReadDto>> GetActivationsAsync(
        [FromQuery] Guid? partnerId,
        [FromQuery] Guid? tenantId) =>
        _readAppService.GetActivationsAsync(new MerchantActivationsQuery
        {
            PartnerId = partnerId,
            TenantId = tenantId,
        });

    [HttpGet("snapshots")]
    public Task<List<SettlementCostMarkupSnapshotReadDto>> GetSnapshotsAsync([FromQuery] Guid? partnerId) =>
        _readAppService.GetSnapshotsAsync(new PartnerCatalogPartnerQuery { PartnerId = partnerId });

    [HttpGet("reflected-orders")]
    public Task<List<ReflectedPartnerOrderReadDto>> GetReflectedOrdersAsync([FromQuery] Guid? partnerId) =>
        _readAppService.GetReflectedOrdersAsync(new PartnerCatalogPartnerQuery { PartnerId = partnerId });

    [HttpGet("platform-catalog-links")]
    public Task<List<PlatformCatalogLinkReadDto>> GetPlatformCatalogLinksAsync(
        [FromQuery] Guid? partnerId,
        [FromQuery] Guid? partnerCatalogItemId) =>
        _readAppService.GetPlatformCatalogLinksAsync(new PlatformCatalogLinksQuery
        {
            PartnerId = partnerId,
            PartnerCatalogItemId = partnerCatalogItemId,
        });
}
