using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.DependencyInjection;

namespace Zahy.Webhooks;

public interface IZahyWebhooksDbSchemaMigrator
{
    Task MigrateAsync();
}

public class ZahyWebhooksDbSchemaMigrator : IZahyWebhooksDbSchemaMigrator, ITransientDependency
{
    private readonly IServiceProvider _serviceProvider;

    public ZahyWebhooksDbSchemaMigrator(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task MigrateAsync()
    {
        await _serviceProvider
            .GetRequiredService<ZahyWebhooksDbContext>()
            .Database
            .MigrateAsync();
    }
}
