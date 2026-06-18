using Volo.Abp.Application;
using Volo.Abp.Modularity;

namespace Zahy.Commission;

[DependsOn(
    typeof(ZahyCommissionDomainModule),
    typeof(ZahyCommissionApplicationContractsModule),
    typeof(AbpDddApplicationModule)
)]
public class ZahyCommissionApplicationModule : AbpModule
{
}