using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.DependencyInjection;

namespace Zahy.Connectors;

public interface IZahyConnectorsDbSchemaMigrator
{
    Task MigrateAsync();
}

public class ZahyConnectorsDbSchemaMigrator : IZahyConnectorsDbSchemaMigrator, ITransientDependency
{
    private readonly IServiceProvider _serviceProvider;

    public ZahyConnectorsDbSchemaMigrator(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task MigrateAsync()
    {
        await _serviceProvider
            .GetRequiredService<ZahyConnectorsDbContext>()
            .Database
            .MigrateAsync();
    }
}
