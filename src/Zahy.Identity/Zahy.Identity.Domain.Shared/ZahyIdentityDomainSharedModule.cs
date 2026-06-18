using Volo.Abp.Modularity;
using Volo.Abp.Validation;

namespace Zahy.Identity;

[DependsOn(typeof(AbpValidationModule))]
public class ZahyIdentityDomainSharedModule : AbpModule
{
}