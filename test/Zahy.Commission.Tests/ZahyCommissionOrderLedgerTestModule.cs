using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Volo.Abp;
using Volo.Abp.Autofac;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore.Sqlite;
using Volo.Abp.Modularity;
using Volo.Abp.Testing;
using Zahy.OrderLedger;
using Zahy.PartnerPlatform.Partners;

namespace Zahy.Commission;

[DependsOn(
    typeof(AbpAutofacModule),
    typeof(AbpTestBaseModule),
    typeof(AbpEntityFrameworkCoreSqliteModule),
    typeof(ZahyCommissionOrderLedgerModule),
    typeof(ZahyOrderLedgerEntityFrameworkCoreModule),
    typeof(ZahyCommissionEntityFrameworkCoreModule)
)]
public class ZahyCommissionOrderLedgerTestModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var sqliteConnection = CreateDatabaseAndGetConnection();

        Configure<AbpDbContextOptions>(options =>
        {
            options.Configure(abpDbContextConfigurationContext =>
            {
                abpDbContextConfigurationContext.DbContextOptions.UseSqlite(sqliteConnection);
            });
        });

        context.Services.Replace(ServiceDescriptor.Singleton<ICommissionPartnerTypeLookup, TestCommissionPartnerTypeLookup>());
    }

    private static SqliteConnection CreateDatabaseAndGetConnection()
    {
        var connection = new ZahyCommissionTestSqliteConnection("Data Source=:memory:");
        connection.Open();

        CreateTables<ZahyOrderLedgerDbContext>(connection);
        CreateTables<ZahyCommissionDbContext>(connection);

        return connection;
    }

    private static void CreateTables<TContext>(SqliteConnection connection)
        where TContext : DbContext
    {
        var options = new DbContextOptionsBuilder<TContext>()
            .UseSqlite(connection)
            .Options;

        using var context = (TContext)Activator.CreateInstance(typeof(TContext), options)!;
        context.GetService<IRelationalDatabaseCreator>().CreateTables();
    }
}

[DependsOn(typeof(ZahyCommissionOrderLedgerTestModule))]
public class ZahyCommissionOrderLedgerIntegrationTestModule : AbpModule
{
}

public class TestCommissionPartnerTypeLookup : ICommissionPartnerTypeLookup
{
    private readonly Dictionary<Guid, PartnerType> _partnerTypes = new();

    public void SetPartnerType(Guid partnerId, PartnerType partnerType) =>
        _partnerTypes[partnerId] = partnerType;

    public Task<PartnerType?> GetPartnerTypeAsync(Guid partnerId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_partnerTypes.TryGetValue(partnerId, out var type) ? type : (PartnerType?)null);
}
