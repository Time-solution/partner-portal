using Volo.Abp.Modularity;
using Volo.Abp.Validation;

namespace Zahy.OrderLedger;

[DependsOn(typeof(AbpValidationModule))]
public class ZahyOrderLedgerDomainSharedModule : AbpModule
{
}