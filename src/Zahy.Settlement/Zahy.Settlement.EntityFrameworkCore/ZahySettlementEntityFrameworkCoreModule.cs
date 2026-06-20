using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.Modularity;

namespace Zahy.Settlement;

[DependsOn(
    typeof(ZahySettlementDomainModule),
    typeof(AbpEntityFrameworkCoreModule)
)]
public class ZahySettlementEntityFrameworkCoreModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddAbpDbContext<ZahySettlementDbContext>(options =>
        {
            options.AddDefaultRepositories(includeAllEntities: true);
        });
    }
}
