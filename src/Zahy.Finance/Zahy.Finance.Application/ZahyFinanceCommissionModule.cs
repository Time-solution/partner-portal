using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Volo.Abp.Modularity;
using Zahy.Commission;

namespace Zahy.Finance;

[DependsOn(
    typeof(ZahyFinanceApplicationModule),
    typeof(ZahyCommissionApplicationModule)
)]
public class ZahyFinanceCommissionModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.Replace(ServiceDescriptor.Transient<ICommissionLedgerFinanceTrigger, CommissionLedgerFinanceTrigger>());
        context.Services.Replace(ServiceDescriptor.Transient<IBillingChargeFinanceTrigger, BillingChargeFinanceTrigger>());
    }
}
