using Volo.Abp.Application;
using Volo.Abp.Authorization;
using Volo.Abp.Identity;
using Volo.Abp.Modularity;
using Volo.Abp.PermissionManagement;

namespace Zahy.Identity;

[DependsOn(
    typeof(ZahyIdentityDomainSharedModule),
    typeof(AbpDddApplicationContractsModule),
    typeof(AbpAuthorizationModule),
    typeof(AbpIdentityApplicationContractsModule),
    typeof(AbpPermissionManagementApplicationContractsModule)
)]
public class ZahyIdentityApplicationContractsModule : AbpModule
{
}
