using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Volo.Abp.Modularity;
using Zahy.OrderLedger;

namespace Zahy.Commission;

[DependsOn(
    typeof(ZahyCommissionApplicationModule),
    typeof(ZahyOrderLedgerApplicationModule)
)]
public class ZahyCommissionOrderLedgerModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.Replace(ServiceDescriptor.Transient<IOrderLedgerCommissionTrigger, OrderLedgerCommissionTrigger>());
    }
}
