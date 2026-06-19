using Volo.Abp.Data;
using Volo.Abp.Domain;
using Volo.Abp.Modularity;

namespace Zahy.Commission;

[DependsOn(
    typeof(ZahyCommissionDomainSharedModule),
    typeof(AbpDddDomainModule)
)]
public class ZahyCommissionDomainModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        Configure<AbpDataFilterOptions>(options =>
        {
            options.DefaultStates[typeof(ICommissionPartnerDataFilter)] = new DataFilterState(isEnabled: true);
        });
    }
}