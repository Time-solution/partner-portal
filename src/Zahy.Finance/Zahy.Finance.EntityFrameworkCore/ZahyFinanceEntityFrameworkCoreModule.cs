using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.EntityFrameworkCore;using Volo.Abp.Modularity;

namespace Zahy.Finance;

[DependsOn(
    typeof(ZahyFinanceDomainModule),
    typeof(AbpEntityFrameworkCoreModule)
)]
public class ZahyFinanceEntityFrameworkCoreModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddAbpDbContext<ZahyFinanceDbContext>(options =>
        {
            options.AddDefaultRepositories(includeAllEntities: true);
        });

        context.Services.AddTransient<ISqlServerFinanceInvoiceNumberAllocator, SqlServerFinanceInvoiceNumberAllocator>();
    }
}
