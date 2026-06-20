using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.Modularity;

namespace Zahy.PartnerCatalog;

[DependsOn(
    typeof(ZahyPartnerCatalogDomainModule),
    typeof(AbpEntityFrameworkCoreModule)
)]
public class ZahyPartnerCatalogEntityFrameworkCoreModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddAbpDbContext<ZahyPartnerCatalogDbContext>(options =>
        {
            options.AddDefaultRepositories(includeAllEntities: true);
        });

        context.Services.AddTransient<IReflectedPartnerOrderStore, ReflectedPartnerOrderStore>();
    }
}
