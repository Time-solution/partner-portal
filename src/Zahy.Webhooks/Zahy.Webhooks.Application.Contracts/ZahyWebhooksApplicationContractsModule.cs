using Volo.Abp.Application;
using Volo.Abp.Authorization;
using Volo.Abp.Modularity;

namespace Zahy.Webhooks;

[DependsOn(
    typeof(ZahyWebhooksDomainSharedModule),
    typeof(AbpDddApplicationContractsModule),
    typeof(AbpAuthorizationModule)
)]
public class ZahyWebhooksApplicationContractsModule : AbpModule
{
}