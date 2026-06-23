using Microsoft.Extensions.DependencyInjection;
using Volo.Abp;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.Modularity;

namespace Zahy.Webhooks;

[DependsOn(
    typeof(ZahyWebhooksDomainModule),
    typeof(AbpEntityFrameworkCoreModule)
)]
public class ZahyWebhooksEntityFrameworkCoreModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddAbpDbContext<ZahyWebhooksDbContext>(options =>
        {
            options.AddDefaultRepositories(includeAllEntities: true);
        });

        context.Services.AddTransient<IWebhookOutboxLeaseService, WebhookOutboxLeaseService>();
    }
}