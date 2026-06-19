using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.Modularity;

namespace Zahy.Connectors;

[DependsOn(
    typeof(ZahyConnectorsDomainModule),
    typeof(AbpEntityFrameworkCoreModule)
)]
public class ZahyConnectorsEntityFrameworkCoreModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddAbpDbContext<ZahyConnectorsDbContext>(options =>
        {
            options.AddDefaultRepositories(includeAllEntities: true);
        });
    }
}
