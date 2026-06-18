using System;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Testing;
using Volo.Abp.Uow;

namespace Zahy.PartnerPlatform;

public abstract class ZahyPartnerPlatformTestBase : AbpIntegratedTest<ZahyPartnerPlatformIntegrationTestModule>
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
