using Volo.Abp.Application;
using Volo.Abp.Authorization;
using Volo.Abp.Modularity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Zahy.Webhooks;

[DependsOn(
    typeof(ZahyWebhooksDomainModule),
    typeof(ZahyWebhooksApplicationContractsModule),
    typeof(AbpDddApplicationModule),
    typeof(AbpAuthorizationModule)
)]
public class ZahyWebhooksApplicationModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.TryAddSingleton<InMemoryWebhookDeliveryTransport>();
        context.Services.AddSingleton<IWebhookDeliveryTransport>(sp =>
            sp.GetRequiredService<InMemoryWebhookDeliveryTransport>());
    }
}