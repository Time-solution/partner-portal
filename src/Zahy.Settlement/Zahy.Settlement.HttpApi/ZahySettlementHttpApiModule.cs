using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.Modularity;

namespace Zahy.Settlement;

[DependsOn(
    typeof(ZahySettlementApplicationContractsModule),
    typeof(AbpAspNetCoreMvcModule)
)]
public class ZahySettlementHttpApiModule : AbpModule
{
}
