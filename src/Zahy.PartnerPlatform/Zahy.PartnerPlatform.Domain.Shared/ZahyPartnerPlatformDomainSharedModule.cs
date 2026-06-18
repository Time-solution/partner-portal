using Volo.Abp.Modularity;
using Volo.Abp.Validation;

namespace Zahy.PartnerPlatform;

[DependsOn(typeof(AbpValidationModule))]
public class ZahyPartnerPlatformDomainSharedModule : AbpModule
{
}