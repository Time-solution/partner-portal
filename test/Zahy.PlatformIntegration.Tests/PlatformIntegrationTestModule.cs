using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Volo.Abp;
using Volo.Abp.Autofac;
using Volo.Abp.Authorization;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore.Sqlite;
using Volo.Abp.Modularity;
using Volo.Abp.Testing;
using Zahy.Identity.Partners;
using Zahy.OrderLedger;
using Zahy.Webhooks;
using Zahy.Connectors;

namespace Zahy.PlatformIntegration;

[DependsOn(
    typeof(AbpAutofacModule),
    typeof(AbpTestBaseModule),
    typeof(AbpAuthorizationModule),
    typeof(AbpEntityFrameworkCoreSqliteModule),
    typeof(ZahyOrderLedgerApplicationModule),
    typeof(ZahyOrderLedgerWebhooksModule),
    typeof(ZahyOrderLedgerEntityFrameworkCoreModule),
    typeof(ZahyConnectorsOrderLedgerModule),
    typeof(ZahyConnectorsEntityFrameworkCoreModule),
    typeof(ZahyWebhooksApplicationModule),
    typeof(ZahyWebhooksEntityFrameworkCoreModule)
)]
public class PlatformIntegrationTestModule : AbpModule
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
        var connection = new PlatformIntegrationTestSqliteConnection("Data Source=:memory:");
        connection.Open();

        CreateTables<ZahyOrderLedgerDbContext>(connection);
        CreateTables<ZahyWebhooksDbContext>(connection);
        CreateTables<ZahyConnectorsDbContext>(connection);

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

[DependsOn(typeof(PlatformIntegrationTestModule))]
public class PlatformIntegrationTestRuntimeModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddAlwaysAllowAuthorization();
        context.Services.AddSingleton<TestCurrentPartner>();
        context.Services.Replace(ServiceDescriptor.Singleton<ICurrentPartner, TestCurrentPartnerAccessor>());
    }
}

public class TestCurrentPartner : ICurrentPartner
{
    public Guid? Id { get; set; }
}

public class TestCurrentPartnerAccessor : ICurrentPartner
{
    private readonly TestCurrentPartner _currentPartner;

    public TestCurrentPartnerAccessor(TestCurrentPartner currentPartner)
    {
        _currentPartner = currentPartner;
    }

    public Guid? Id => _currentPartner.Id;
}
