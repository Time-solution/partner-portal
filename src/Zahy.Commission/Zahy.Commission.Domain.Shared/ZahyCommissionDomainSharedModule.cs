using Volo.Abp.Modularity;
using Volo.Abp.Validation;

namespace Zahy.Commission;

[DependsOn(typeof(AbpValidationModule))]
public class ZahyCommissionDomainSharedModule : AbpModule
{
}