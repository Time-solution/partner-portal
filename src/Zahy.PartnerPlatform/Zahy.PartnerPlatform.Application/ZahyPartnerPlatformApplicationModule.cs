using Volo.Abp.Application;
using Volo.Abp.Authorization;
using Volo.Abp.Identity;
using Volo.Abp.Modularity;
using Volo.Abp.Validation;

namespace Zahy.PartnerPlatform;

[DependsOn(
    typeof(ZahyPartnerPlatformDomainModule),
    typeof(ZahyPartnerPlatformApplicationContractsModule),
    typeof(AbpDddApplicationModule),
    typeof(AbpValidationModule),
    typeof(AbpAuthorizationModule)
)]
public class ZahyPartnerPlatformApplicationModule : AbpModule
{
}