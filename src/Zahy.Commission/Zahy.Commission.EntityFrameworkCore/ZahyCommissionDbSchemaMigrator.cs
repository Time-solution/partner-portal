using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.DependencyInjection;

namespace Zahy.Commission;

public interface IZahyCommissionDbSchemaMigrator
{
    Task MigrateAsync();
}

public class ZahyCommissionDbSchemaMigrator : IZahyCommissionDbSchemaMigrator, ITransientDependency
{
    private readonly IServiceProvider _serviceProvider;

    public ZahyCommissionDbSchemaMigrator(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task MigrateAsync()
    {
        await _serviceProvider
            .GetRequiredService<ZahyCommissionDbContext>()
            .Database
            .MigrateAsync();
    }
}
