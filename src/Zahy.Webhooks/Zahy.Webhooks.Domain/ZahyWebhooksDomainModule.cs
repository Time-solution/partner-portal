using Volo.Abp.Domain;
using Volo.Abp.Modularity;

namespace Zahy.Webhooks;

[DependsOn(
    typeof(ZahyWebhooksDomainSharedModule),
    typeof(AbpDddDomainModule)
)]
public class ZahyWebhooksDomainModule : AbpModule
{
}