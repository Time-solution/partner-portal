using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Volo.Abp.Application;
using Volo.Abp.Modularity;

namespace Zahy.OrderLedger;

[DependsOn(
    typeof(ZahyOrderLedgerDomainModule),
    typeof(ZahyOrderLedgerApplicationContractsModule),
    typeof(AbpDddApplicationModule)
)]
public class ZahyOrderLedgerApplicationModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.TryAddSingleton<FakeOrderSource>();
        context.Services.AddSingleton<IOrderSource>(sp => sp.GetRequiredService<FakeOrderSource>());
        context.Services.AddTransient<IOrderLedgerWebhookNotifier, NullOrderLedgerWebhookNotifier>();
        context.Services.AddTransient<IOrderLedgerCommissionTrigger, NullOrderLedgerCommissionTrigger>();
    }
}