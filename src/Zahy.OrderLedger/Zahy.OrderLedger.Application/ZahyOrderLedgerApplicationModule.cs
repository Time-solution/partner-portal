using Volo.Abp.Application;
using Volo.Abp.Modularity;

namespace Zahy.OrderLedger;

[DependsOn(
    typeof(ZahyOrderLedgerDomainModule),
    typeof(ZahyOrderLedgerApplicationContractsModule),
    typeof(AbpDddApplicationModule)
)]
public class ZahyOrderLedgerApplicationModule : AbpModule
{
}