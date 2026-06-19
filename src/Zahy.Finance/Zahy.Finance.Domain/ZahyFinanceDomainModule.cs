using Volo.Abp.Data;
using Volo.Abp.Domain;
using Volo.Abp.Modularity;

namespace Zahy.Finance;

[DependsOn(
    typeof(ZahyFinanceDomainSharedModule),
    typeof(AbpDddDomainModule)
)]
public class ZahyFinanceDomainModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        Configure<AbpDataFilterOptions>(options =>
        {
            options.DefaultStates[typeof(IFinancePartnerDataFilter)] = new DataFilterState(isEnabled: true);
            options.DefaultStates[typeof(IFinanceTenantDataFilter)] = new DataFilterState(isEnabled: true);
        });
    }
}
