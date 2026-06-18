using Volo.Abp.Application;
using Volo.Abp.Authorization;
using Volo.Abp.Modularity;

namespace Zahy.Commission;

[DependsOn(
    typeof(ZahyCommissionDomainSharedModule),
    typeof(AbpDddApplicationContractsModule),
    typeof(AbpAuthorizationModule)
)]
public class ZahyCommissionApplicationContractsModule : AbpModule
{
}