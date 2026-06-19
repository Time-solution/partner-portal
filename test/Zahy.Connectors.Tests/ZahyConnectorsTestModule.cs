using Volo.Abp;
using Volo.Abp.Autofac;
using Volo.Abp.Modularity;
using Volo.Abp.Testing;

namespace Zahy.Connectors;

[DependsOn(
    typeof(AbpAutofacModule),
    typeof(AbpTestBaseModule),
    typeof(ZahyConnectorsApplicationModule)
)]
public class ZahyConnectorsTestModule : AbpModule
{
}

[DependsOn(typeof(ZahyConnectorsTestModule))]
public class ZahyConnectorsIntegrationTestModule : AbpModule
{
}
