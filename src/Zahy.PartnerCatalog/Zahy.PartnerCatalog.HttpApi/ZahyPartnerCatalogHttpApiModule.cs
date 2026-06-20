using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.Modularity;

namespace Zahy.PartnerCatalog;

[DependsOn(
    typeof(ZahyPartnerCatalogApplicationContractsModule),
    typeof(AbpAspNetCoreMvcModule)
)]
public class ZahyPartnerCatalogHttpApiModule : AbpModule
{
}
