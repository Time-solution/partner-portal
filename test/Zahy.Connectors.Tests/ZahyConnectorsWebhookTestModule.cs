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
using Zahy.Identity.Partners;
using Zahy.OrderLedger;
using Zahy.Webhooks;

namespace Zahy.Connectors;

[DependsOn(
    typeof(AbpAutofacModule),
    typeof(AbpTestBaseModule),
    typeof(AbpEntityFrameworkCoreSqliteModule),
    typeof(ZahyConnectorsOrderLedgerModule),
    typeof(ZahyOrderLedgerWebhooksModule),
    typeof(ZahyOrderLedgerEntityFrameworkCoreModule),
    typeof(ZahyWebhooksApplicationModule),
    typeof(ZahyWebhooksEntityFrameworkCoreModule)
)]
public class ZahyConnectorsWebhookTestModule : AbpModule
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
        var connection = new ConnectorsWebhookTestSqliteConnection("Data Source=:memory:");
        connection.Open();

        CreateTables<ZahyOrderLedgerDbContext>(connection);
        CreateTables<ZahyWebhooksDbContext>(connection);

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

[DependsOn(typeof(ZahyConnectorsWebhookTestModule))]
public class ZahyConnectorsWebhookIntegrationTestModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddSingleton<ConnectorsTestCurrentPartner>();
        context.Services.Replace(ServiceDescriptor.Singleton<ICurrentPartner, ConnectorsTestCurrentPartnerAccessor>());
    }
}

internal sealed class ConnectorsWebhookTestSqliteConnection : SqliteConnection
{
    public ConnectorsWebhookTestSqliteConnection(string connectionString)
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

public class ConnectorsTestCurrentPartner : ICurrentPartner
{
    public Guid? Id { get; set; }
}

public class ConnectorsTestCurrentPartnerAccessor : ICurrentPartner
{
    private readonly ConnectorsTestCurrentPartner _currentPartner;

    public ConnectorsTestCurrentPartnerAccessor(ConnectorsTestCurrentPartner currentPartner)
    {
        _currentPartner = currentPartner;
    }

    public Guid? Id => _currentPartner.Id;
}
