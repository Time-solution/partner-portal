using Volo.Abp.Application;
using Volo.Abp.Modularity;

namespace Zahy.Identity;

[DependsOn(
    typeof(ZahyIdentityDomainModule),
    typeof(ZahyIdentityApplicationContractsModule),
    typeof(AbpDddApplicationModule)
)]
public class ZahyIdentityApplicationModule : AbpModule
{
}