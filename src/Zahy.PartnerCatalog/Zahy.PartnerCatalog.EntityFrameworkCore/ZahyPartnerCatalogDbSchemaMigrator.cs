using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.DependencyInjection;

namespace Zahy.PartnerCatalog;

public interface IZahyPartnerCatalogDbSchemaMigrator
{
    Task MigrateAsync();
}

/// <summary>
/// Applies Partner Catalog schema. Intentionally NOT wired into Zahy.DbMigrator in Step 2a.
/// </summary>
public class ZahyPartnerCatalogDbSchemaMigrator : ITransientDependency, IZahyPartnerCatalogDbSchemaMigrator
{
    private readonly IServiceProvider _serviceProvider;

    public ZahyPartnerCatalogDbSchemaMigrator(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task MigrateAsync()
    {
        await _serviceProvider
            .GetRequiredService<ZahyPartnerCatalogDbContext>()
            .Database
            .MigrateAsync();
    }
}
