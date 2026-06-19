using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.DependencyInjection;

namespace Zahy.OrderLedger;

public interface IZahyOrderLedgerDbSchemaMigrator
{
    Task MigrateAsync();
}

public class ZahyOrderLedgerDbSchemaMigrator : IZahyOrderLedgerDbSchemaMigrator, ITransientDependency
{
    private readonly IServiceProvider _serviceProvider;

    public ZahyOrderLedgerDbSchemaMigrator(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task MigrateAsync()
    {
        await _serviceProvider
            .GetRequiredService<ZahyOrderLedgerDbContext>()
            .Database
            .MigrateAsync();
    }
}
