using Volo.Abp.Application;
using Volo.Abp.Authorization;
using Volo.Abp.Modularity;

namespace Zahy.PartnerPlatform;

[DependsOn(
    typeof(ZahyPartnerPlatformDomainSharedModule),
    typeof(AbpDddApplicationContractsModule),
    typeof(AbpAuthorizationModule)
)]
public class ZahyPartnerPlatformApplicationContractsModule : AbpModule
{
}