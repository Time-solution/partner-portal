using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.Modularity;

namespace Zahy.Identity;

[DependsOn(
    typeof(ZahyIdentityApplicationContractsModule),
    typeof(AbpAspNetCoreMvcModule)
)]
public class ZahyIdentityHttpApiModule : AbpModule
{
}