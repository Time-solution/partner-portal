using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Volo.Abp;
using Volo.Abp.Autofac;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore.Sqlite;
using Volo.Abp.Modularity;
using Volo.Abp.Testing;
using Zahy.OrderLedger;

namespace Zahy.Connectors;

[DependsOn(
    typeof(AbpAutofacModule),
    typeof(AbpTestBaseModule),
    typeof(AbpEntityFrameworkCoreSqliteModule),
    typeof(ZahyConnectorsOrderLedgerModule),
    typeof(ZahyOrderLedgerEntityFrameworkCoreModule)
)]
public class ZahyConnectorsOrderLedgerTestModule : AbpModule
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
    }

    private static SqliteConnection CreateDatabaseAndGetConnection()
    {
        var connection = new ConnectorsTestSqliteConnection("Data Source=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<ZahyOrderLedgerDbContext>()
            .UseSqlite(connection)
            .Options;

        using var context = new ZahyOrderLedgerDbContext(options);
        context.GetService<IRelationalDatabaseCreator>().CreateTables();

        return connection;
    }
}

[DependsOn(typeof(ZahyConnectorsOrderLedgerTestModule))]
public class ZahyConnectorsOrderLedgerIntegrationTestModule : AbpModule
{
}

internal sealed class ConnectorsTestSqliteConnection : SqliteConnection
{
    public ConnectorsTestSqliteConnection(string connectionString)
        : base(connectionString)
    {
    }

    public override void Close()
    {
    }

    protected override void Dispose(bool disposing)
    {
    }
}
