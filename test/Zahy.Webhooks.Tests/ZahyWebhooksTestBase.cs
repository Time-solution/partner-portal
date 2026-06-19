using System;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Testing;
using Volo.Abp.Uow;
using Zahy.Identity.Partners;

namespace Zahy.Webhooks;

public class TestCurrentPartner : ICurrentPartner
{
    public Guid? Id { get; set; }
}

public class TestCurrentPartnerAccessor : ICurrentPartner
{
    private readonly TestCurrentPartner _currentPartner;

    public TestCurrentPartnerAccessor(TestCurrentPartner currentPartner)
    {
        _currentPartner = currentPartner;
    }

    public Guid? Id => _currentPartner.Id;
}

public abstract class ZahyWebhooksTestBase : AbpIntegratedTest<ZahyWebhooksIntegrationTestModule>
{
    protected override void SetAbpApplicationCreationOptions(AbpApplicationCreationOptions options)
    {
        options.UseAutofac();
    }

    protected virtual async Task WithUnitOfWorkAsync(Func<Task> action)
    {
        var uowManager = GetRequiredService<IUnitOfWorkManager>();
        using var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true);
        await action();
        await uow.CompleteAsync();
    }
}
