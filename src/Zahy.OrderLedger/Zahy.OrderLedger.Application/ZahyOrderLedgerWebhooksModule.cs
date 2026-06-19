using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Volo.Abp.Modularity;
using Zahy.Webhooks;

namespace Zahy.OrderLedger;

/// <summary>Activates ledger → webhook outbox wiring when both modules are loaded.</summary>
[DependsOn(
    typeof(ZahyOrderLedgerApplicationModule),
    typeof(ZahyWebhooksApplicationContractsModule)
)]
public class ZahyOrderLedgerWebhooksModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.Replace(ServiceDescriptor.Transient<IOrderLedgerWebhookNotifier, OrderLedgerWebhookNotifier>());
    }
}
