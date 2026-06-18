using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.DependencyInjection;

namespace Zahy.Identity.EntityFrameworkCore;

public interface IZahyIdentityDbSchemaMigrator
{
    Task MigrateAsync();
}

public class ZahyIdentityDbSchemaMigrator : IZahyIdentityDbSchemaMigrator, ITransientDependency
{
    private readonly IServiceProvider _serviceProvider;

    public ZahyIdentityDbSchemaMigrator(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task MigrateAsync()
    {
        await _serviceProvider
            .GetRequiredService<ZahyIdentityDbContext>()
            .Database
            .MigrateAsync();
    }
}
