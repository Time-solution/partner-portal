using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.DependencyInjection;

namespace Zahy.Finance;

public interface IZahyFinanceDbSchemaMigrator
{
    Task MigrateAsync();
}

public class ZahyFinanceDbSchemaMigrator : IZahyFinanceDbSchemaMigrator, ITransientDependency
{
    private readonly IServiceProvider _serviceProvider;

    public ZahyFinanceDbSchemaMigrator(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task MigrateAsync()
    {
        await _serviceProvider
            .GetRequiredService<ZahyFinanceDbContext>()
            .Database
            .MigrateAsync();
    }
}
