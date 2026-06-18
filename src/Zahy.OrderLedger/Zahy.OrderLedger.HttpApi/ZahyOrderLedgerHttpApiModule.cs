using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.Modularity;

namespace Zahy.OrderLedger;

[DependsOn(
    typeof(ZahyOrderLedgerApplicationContractsModule),
    typeof(AbpAspNetCoreMvcModule)
)]
public class ZahyOrderLedgerHttpApiModule : AbpModule
{
}