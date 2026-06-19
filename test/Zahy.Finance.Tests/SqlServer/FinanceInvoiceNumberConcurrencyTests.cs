using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Testing;
using Volo.Abp.Uow;
using Xunit;

namespace Zahy.Finance;

public class FinanceInvoiceNumberConcurrencyTests : AbpIntegratedTest<ZahyFinanceSqlServerTestModule>
{
    protected override void SetAbpApplicationCreationOptions(AbpApplicationCreationOptions options)
    {
        options.UseAutofac();
    }

    [Fact]
    public async Task Concurrent_Invoice_Generations_Are_Gapless_And_Unique()
    {
        if (!SqlServerTestEnvironment.IsAvailable())
        {
            return;
        }

        await SqlServerTestEnvironment.EnsureFinanceSchemaAsync(ServiceProvider);

        var results = await Task.WhenAll(
            Task.Run(AllocateInNewUnitOfWorkAsync),
            Task.Run(AllocateInNewUnitOfWorkAsync));

        results[0].InvoiceNumber.ShouldNotBe(results[1].InvoiceNumber);
        var numbers = results.Select(x => x.SequenceNumber).OrderBy(x => x).ToArray();
        numbers[1].ShouldBe(numbers[0] + 1);

        await WithUnitOfWorkAsync(async () =>
        {
            var sequenceRepository = GetRequiredService<IRepository<InvoiceNumberSequence, Guid>>();
            var sequence = (await sequenceRepository.GetListAsync()).Single(x =>
                x.DocumentKind == FinanceDocumentKind.Invoice &&
                x.FiscalYear == DateTime.UtcNow.Year);
            sequence.LastNumber.ShouldBeGreaterThanOrEqualTo(numbers[1]);
        });
    }

    private async Task<FinanceInvoiceNumberAllocation> AllocateInNewUnitOfWorkAsync()
    {
        using var scope = ServiceProvider.CreateScope();
        var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
        using var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true);
        var allocator = scope.ServiceProvider.GetRequiredService<IFinanceInvoiceNumberAllocator>();
        var allocation = await allocator.AllocateInvoiceNumberAsync(DateTime.UtcNow);
        await uow.CompleteAsync();
        return allocation;
    }

    private async Task WithUnitOfWorkAsync(Func<Task> action)
    {
        var uowManager = GetRequiredService<IUnitOfWorkManager>();
        using var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true);
        await action();
        await uow.CompleteAsync();
    }
}
