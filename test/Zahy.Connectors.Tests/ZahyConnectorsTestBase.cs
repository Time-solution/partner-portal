using Volo.Abp;
using Volo.Abp.Testing;

namespace Zahy.Connectors;

public abstract class ZahyConnectorsTestBase : AbpIntegratedTest<ZahyConnectorsIntegrationTestModule>
{
    protected override void SetAbpApplicationCreationOptions(AbpApplicationCreationOptions options)
    {
        options.UseAutofac();
    }
}
