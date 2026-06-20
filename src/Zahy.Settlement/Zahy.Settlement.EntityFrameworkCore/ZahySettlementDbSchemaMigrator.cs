using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.DependencyInjection;

namespace Zahy.Settlement;

public interface IZahySettlementDbSchemaMigrator
{
    Task MigrateAsync();
}

/// <summary>
/// Applies the Settlement schema. NOTE: this is intentionally NOT wired into Zahy.DbMigrator yet,
/// so it is never invoked against any real database until the engine is hosted and signed off.
/// </summary>
public class ZahySettlementDbSchemaMigrator : ITransientDependency, IZahySettlementDbSchemaMigrator
{
    private readonly IServiceProvider _serviceProvider;

    public ZahySettlementDbSchemaMigrator(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task MigrateAsync()
    {
        await _serviceProvider
            .GetRequiredService<ZahySettlementDbContext>()
            .Database
            .MigrateAsync();
    }
}
