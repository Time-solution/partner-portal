using Microsoft.Extensions.DependencyInjection;
using Volo.Abp;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.Modularity;

namespace Zahy.PartnerPlatform;

[DependsOn(
    typeof(ZahyPartnerPlatformDomainModule),
    typeof(AbpEntityFrameworkCoreModule)
)]
public class ZahyPartnerPlatformEntityFrameworkCoreModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddAbpDbContext<ZahyPartnerPlatformDbContext>(options =>
        {
            options.AddDefaultRepositories(includeAllEntities: true);
        });
    }
}