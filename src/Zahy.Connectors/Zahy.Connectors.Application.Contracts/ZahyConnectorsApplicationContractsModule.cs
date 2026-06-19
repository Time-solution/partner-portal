using Volo.Abp.Application;
using Volo.Abp.Modularity;

namespace Zahy.Connectors;

[DependsOn(
    typeof(ZahyConnectorsDomainSharedModule),
    typeof(AbpDddApplicationContractsModule)
)]
public class ZahyConnectorsApplicationContractsModule : AbpModule
{
}
