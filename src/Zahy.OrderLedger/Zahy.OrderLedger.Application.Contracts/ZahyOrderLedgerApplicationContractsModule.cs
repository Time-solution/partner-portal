using Volo.Abp.Application;
using Volo.Abp.Authorization;
using Volo.Abp.Modularity;

namespace Zahy.OrderLedger;

[DependsOn(
    typeof(ZahyOrderLedgerDomainSharedModule),
    typeof(AbpDddApplicationContractsModule),
    typeof(AbpAuthorizationModule)
)]
public class ZahyOrderLedgerApplicationContractsModule : AbpModule
{
}