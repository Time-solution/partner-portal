using Volo.Abp.Data;
using Volo.Abp.Domain;
using Volo.Abp.Modularity;

namespace Zahy.Connectors;

[DependsOn(
    typeof(ZahyConnectorsDomainSharedModule),
    typeof(AbpDddDomainModule)
)]
public class ZahyConnectorsDomainModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        Configure<AbpDataFilterOptions>(options =>
        {
            options.DefaultStates[typeof(IConnectorPartnerDataFilter)] = new DataFilterState(isEnabled: true);
        });
    }
}
