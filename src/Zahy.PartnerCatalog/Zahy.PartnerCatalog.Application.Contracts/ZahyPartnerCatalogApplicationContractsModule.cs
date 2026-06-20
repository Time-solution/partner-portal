using Volo.Abp.Application;
using Volo.Abp.Modularity;

namespace Zahy.PartnerCatalog;

[DependsOn(typeof(AbpDddApplicationContractsModule))]
public class ZahyPartnerCatalogApplicationContractsModule : AbpModule
{
}
