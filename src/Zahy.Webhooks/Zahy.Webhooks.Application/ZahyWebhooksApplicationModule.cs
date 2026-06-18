using Volo.Abp.Application;
using Volo.Abp.Modularity;

namespace Zahy.Webhooks;

[DependsOn(
    typeof(ZahyWebhooksDomainModule),
    typeof(ZahyWebhooksApplicationContractsModule),
    typeof(AbpDddApplicationModule)
)]
public class ZahyWebhooksApplicationModule : AbpModule
{
}