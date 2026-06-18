using Volo.Abp.Identity;
using Volo.Abp.Modularity;
using Volo.Abp.OpenIddict;
using Volo.Abp.Validation;

namespace Zahy.Identity;

[DependsOn(
    typeof(AbpValidationModule),
    typeof(AbpIdentityDomainSharedModule),
    typeof(AbpOpenIddictDomainSharedModule)
)]
public class ZahyIdentityDomainSharedModule : AbpModule
{
}
