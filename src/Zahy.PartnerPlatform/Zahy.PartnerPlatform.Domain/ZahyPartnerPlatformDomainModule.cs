using Volo.Abp.Domain;
using Volo.Abp.Modularity;

namespace Zahy.PartnerPlatform;

[DependsOn(
    typeof(ZahyPartnerPlatformDomainSharedModule),
    typeof(AbpDddDomainModule)
)]
public class ZahyPartnerPlatformDomainModule : AbpModule
{
}