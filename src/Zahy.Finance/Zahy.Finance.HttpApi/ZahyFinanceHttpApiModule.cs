using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.Modularity;

namespace Zahy.Finance;

[DependsOn(
    typeof(ZahyFinanceApplicationContractsModule),
    typeof(AbpAspNetCoreMvcModule)
)]
public class ZahyFinanceHttpApiModule : AbpModule
{
}
