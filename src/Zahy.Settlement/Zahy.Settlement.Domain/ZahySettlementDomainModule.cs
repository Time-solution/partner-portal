using Volo.Abp.Domain;
using Volo.Abp.Modularity;

namespace Zahy.Settlement;

[DependsOn(
    typeof(ZahySettlementDomainSharedModule),
    typeof(AbpDddDomainModule)
)]
public class ZahySettlementDomainModule : AbpModule
{
}
