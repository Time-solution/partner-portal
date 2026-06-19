using Volo.Abp.Application;
using Volo.Abp.Modularity;

namespace Zahy.Finance;

[DependsOn(
    typeof(ZahyFinanceDomainSharedModule),
    typeof(AbpDddApplicationContractsModule)
)]
public class ZahyFinanceApplicationContractsModule : AbpModule
{
}
