using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace Zahy.PartnerCatalog.Read;

public interface IPartnerCatalogReadAppService : IApplicationService
{
    Task<List<PartnerCatalogItemReadDto>> GetCatalogItemsAsync(PartnerCatalogItemsQuery query);

    Task<List<MerchantActivationReadDto>> GetActivationsAsync(MerchantActivationsQuery query);

    Task<List<SettlementCostMarkupSnapshotReadDto>> GetSnapshotsAsync(PartnerCatalogPartnerQuery query);

    Task<List<ReflectedPartnerOrderReadDto>> GetReflectedOrdersAsync(PartnerCatalogPartnerQuery query);

    Task<List<PlatformCatalogLinkReadDto>> GetPlatformCatalogLinksAsync(PlatformCatalogLinksQuery query);
}
