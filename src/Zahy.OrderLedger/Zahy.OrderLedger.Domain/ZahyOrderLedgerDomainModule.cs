using Volo.Abp.Domain;
using Volo.Abp.Modularity;

namespace Zahy.OrderLedger;

[DependsOn(
    typeof(ZahyOrderLedgerDomainSharedModule),
    typeof(AbpDddDomainModule)
)]
public class ZahyOrderLedgerDomainModule : AbpModule
{
}