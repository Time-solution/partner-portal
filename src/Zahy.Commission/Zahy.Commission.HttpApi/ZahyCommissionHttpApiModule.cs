using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.Modularity;

namespace Zahy.Commission;

[DependsOn(
    typeof(ZahyCommissionApplicationContractsModule),
    typeof(AbpAspNetCoreMvcModule)
)]
public class ZahyCommissionHttpApiModule : AbpModule
{
}