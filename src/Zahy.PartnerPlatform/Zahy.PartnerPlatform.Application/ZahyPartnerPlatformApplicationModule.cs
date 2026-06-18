using Volo.Abp.Application;
using Volo.Abp.Modularity;

namespace Zahy.PartnerPlatform;

[DependsOn(
    typeof(ZahyPartnerPlatformDomainModule),
    typeof(ZahyPartnerPlatformApplicationContractsModule),
    typeof(AbpDddApplicationModule)
)]
public class ZahyPartnerPlatformApplicationModule : AbpModule
{
}