using Volo.Abp.Modularity;
using Volo.Abp.Validation;

namespace Zahy.Settlement;

[DependsOn(typeof(AbpValidationModule))]
public class ZahySettlementDomainSharedModule : AbpModule
{
}
