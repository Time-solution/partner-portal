using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.Identity;
using Volo.Abp.Modularity;
using Volo.Abp.PermissionManagement.HttpApi;

namespace Zahy.Identity;

[DependsOn(
    typeof(ZahyIdentityApplicationContractsModule),
    typeof(AbpAspNetCoreMvcModule),
    typeof(AbpIdentityHttpApiModule),
    typeof(AbpPermissionManagementHttpApiModule)
)]
public class ZahyIdentityHttpApiModule : AbpModule
{
}
