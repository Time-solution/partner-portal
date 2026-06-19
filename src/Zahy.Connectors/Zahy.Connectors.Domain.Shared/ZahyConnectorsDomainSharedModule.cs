using Volo.Abp.Modularity;
using Volo.Abp.Validation;

namespace Zahy.Connectors;

[DependsOn(typeof(AbpValidationModule))]
public class ZahyConnectorsDomainSharedModule : AbpModule
{
}
