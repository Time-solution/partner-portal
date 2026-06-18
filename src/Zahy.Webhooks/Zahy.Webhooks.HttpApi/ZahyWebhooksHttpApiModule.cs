using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.Modularity;

namespace Zahy.Webhooks;

[DependsOn(
    typeof(ZahyWebhooksApplicationContractsModule),
    typeof(AbpAspNetCoreMvcModule)
)]
public class ZahyWebhooksHttpApiModule : AbpModule
{
}