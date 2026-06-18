using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.Modularity;

namespace Zahy.PartnerPlatform;

[DependsOn(
    typeof(ZahyPartnerPlatformApplicationContractsModule),
    typeof(AbpAspNetCoreMvcModule)
)]
public class ZahyPartnerPlatformHttpApiModule : AbpModule
{
}