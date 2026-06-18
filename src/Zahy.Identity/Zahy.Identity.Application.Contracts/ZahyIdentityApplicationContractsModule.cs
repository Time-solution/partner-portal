using Volo.Abp.Application;
using Volo.Abp.Authorization;
using Volo.Abp.Modularity;

namespace Zahy.Identity;

[DependsOn(
    typeof(ZahyIdentityDomainSharedModule),
    typeof(AbpDddApplicationContractsModule),
    typeof(AbpAuthorizationModule)
)]
public class ZahyIdentityApplicationContractsModule : AbpModule
{
}