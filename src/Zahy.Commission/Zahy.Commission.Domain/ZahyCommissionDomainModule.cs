using Volo.Abp.Domain;
using Volo.Abp.Modularity;

namespace Zahy.Commission;

[DependsOn(
    typeof(ZahyCommissionDomainSharedModule),
    typeof(AbpDddDomainModule)
)]
public class ZahyCommissionDomainModule : AbpModule
{
}