using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.DependencyInjection;

namespace Zahy.PartnerPlatform.EntityFrameworkCore;

public interface IZahyPartnerPlatformDbSchemaMigrator
{
    Task MigrateAsync();
}

public class ZahyPartnerPlatformDbSchemaMigrator : IZahyPartnerPlatformDbSchemaMigrator, ITransientDependency
{
    private readonly IServiceProvider _serviceProvider;

    public ZahyPartnerPlatformDbSchemaMigrator(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task MigrateAsync()
    {
        await _serviceProvider
            .GetRequiredService<ZahyPartnerPlatformDbContext>()
            .Database
            .MigrateAsync();
    }
}
