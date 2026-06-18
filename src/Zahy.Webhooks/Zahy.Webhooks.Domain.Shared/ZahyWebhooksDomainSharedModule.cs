using Volo.Abp.Modularity;
using Volo.Abp.Validation;

namespace Zahy.Webhooks;

[DependsOn(typeof(AbpValidationModule))]
public class ZahyWebhooksDomainSharedModule : AbpModule
{
}