using Volo.Abp.Data;
using Volo.Abp.Domain;
using Volo.Abp.Modularity;

namespace Zahy.Webhooks;

[DependsOn(
    typeof(ZahyWebhooksDomainSharedModule),
    typeof(AbpDddDomainModule)
)]
public class ZahyWebhooksDomainModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        Configure<AbpDataFilterOptions>(options =>
        {
            options.DefaultStates[typeof(IWebhookPartnerDataFilter)] = new DataFilterState(isEnabled: true);
        });
    }
}