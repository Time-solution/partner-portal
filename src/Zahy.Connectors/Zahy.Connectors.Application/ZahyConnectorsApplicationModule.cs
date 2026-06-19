using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.Application;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Modularity;

namespace Zahy.Connectors;

[DependsOn(
    typeof(ZahyConnectorsApplicationContractsModule),
    typeof(AbpDddApplicationModule)
)]
public class ZahyConnectorsApplicationModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddSingleton<InMemoryAggregatorConnectorTransport>();
        context.Services.AddSingleton<InMemoryThreePLConnectorTransport>();
        context.Services.AddSingleton<InMemoryCarrierConnectorTransport>();
        context.Services.AddTransient<IAcceptancePolicyEvaluator, AcceptancePolicyEvaluator>();
        context.Services.AddSingleton<MockAggregatorConnector>();
        context.Services.AddSingleton<MockThreePLConnector>();
        context.Services.AddSingleton<MockCarrierConnector>();
        context.Services.AddSingleton<IConnectorRegistry>(sp => new ConnectorRegistry([
            sp.GetRequiredService<MockAggregatorConnector>(),
            sp.GetRequiredService<MockThreePLConnector>(),
            sp.GetRequiredService<MockCarrierConnector>()
        ]));
        context.Services.AddTransient<ICanonicalOrderMapper, CanonicalOrderMapper>();
    }
}
