using Volo.Abp.Application;
using Volo.Abp.Modularity;

namespace Zahy.Settlement;

[DependsOn(typeof(AbpDddApplicationContractsModule))]
public class ZahySettlementApplicationContractsModule : AbpModule
{
}
