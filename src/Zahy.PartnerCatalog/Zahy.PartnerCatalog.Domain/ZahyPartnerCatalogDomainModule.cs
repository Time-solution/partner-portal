using Volo.Abp.Data;
using Volo.Abp.Domain;
using Volo.Abp.Modularity;

namespace Zahy.PartnerCatalog;

[DependsOn(
    typeof(ZahyPartnerCatalogDomainSharedModule),
    typeof(AbpDddDomainModule)
)]
public class ZahyPartnerCatalogDomainModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        Configure<AbpDataFilterOptions>(options =>
        {
            options.DefaultStates[typeof(IPartnerCatalogDataFilter)] = new DataFilterState(isEnabled: true);
        });
    }
}
