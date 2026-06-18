using Volo.Abp.Domain;
using Volo.Abp.Modularity;

namespace Zahy.Identity;

[DependsOn(
    typeof(ZahyIdentityDomainSharedModule),
    typeof(AbpDddDomainModule)
)]
public class ZahyIdentityDomainModule : AbpModule
{
}