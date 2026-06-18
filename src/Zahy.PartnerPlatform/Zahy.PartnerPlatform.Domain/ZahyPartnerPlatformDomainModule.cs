using Volo.Abp.Data;
using Volo.Abp.Domain;
using Volo.Abp.Modularity;
using Zahy.PartnerPlatform.Partners;

namespace Zahy.PartnerPlatform;

[DependsOn(
    typeof(ZahyPartnerPlatformDomainSharedModule),
    typeof(AbpDddDomainModule)
)]
public class ZahyPartnerPlatformDomainModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        Configure<AbpDataFilterOptions>(options =>
        {
            options.DefaultStates[typeof(IPartnerDataFilter)] = new DataFilterState(isEnabled: true);
        });
    }
}
