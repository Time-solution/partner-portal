using Volo.Abp.Application;
using Volo.Abp.Identity;
using Volo.Abp.Identity.AspNetCore;
using Volo.Abp.Modularity;
using Volo.Abp.PermissionManagement;

namespace Zahy.Identity;

[DependsOn(
    typeof(ZahyIdentityDomainModule),
    typeof(ZahyIdentityApplicationContractsModule),
    typeof(AbpDddApplicationModule),
    typeof(AbpIdentityApplicationModule),
    typeof(AbpIdentityAspNetCoreModule),
    typeof(AbpPermissionManagementApplicationModule)
)]
public class ZahyIdentityApplicationModule : AbpModule
{
}
