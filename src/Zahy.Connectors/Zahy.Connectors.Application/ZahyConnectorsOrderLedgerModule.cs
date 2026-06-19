using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.Modularity;
using Zahy.OrderLedger;

namespace Zahy.Connectors;

[DependsOn(
    typeof(ZahyConnectorsApplicationModule),
    typeof(ZahyOrderLedgerApplicationModule)
)]
public class ZahyConnectorsOrderLedgerModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddTransient<IConnectorOrderIngestionService, ConnectorOrderIngestionService>();
    }
}
