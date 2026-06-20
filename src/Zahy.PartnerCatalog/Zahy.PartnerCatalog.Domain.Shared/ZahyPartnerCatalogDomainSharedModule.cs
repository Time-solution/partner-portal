using Volo.Abp.Modularity;
using Volo.Abp.Validation;

namespace Zahy.PartnerCatalog;

[DependsOn(typeof(AbpValidationModule))]
public class ZahyPartnerCatalogDomainSharedModule : AbpModule
{
}
