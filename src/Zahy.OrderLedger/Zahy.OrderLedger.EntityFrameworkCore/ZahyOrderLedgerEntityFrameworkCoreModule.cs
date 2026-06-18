using Microsoft.Extensions.DependencyInjection;
using Volo.Abp;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.Modularity;

namespace Zahy.OrderLedger;

[DependsOn(
    typeof(ZahyOrderLedgerDomainModule),
    typeof(AbpEntityFrameworkCoreModule)
)]
public class ZahyOrderLedgerEntityFrameworkCoreModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddAbpDbContext<ZahyOrderLedgerDbContext>(options =>
        {
            options.AddDefaultRepositories(includeAllEntities: true);
        });
    }
}