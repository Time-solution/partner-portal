using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.Autofac;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore.Sqlite;
using Volo.Abp.Modularity;
using Volo.Abp.Testing;
using Volo.Abp.Users;

namespace Zahy.Commission;

[DependsOn(
    typeof(AbpAutofacModule),
    typeof(AbpTestBaseModule),
    typeof(AbpAuthorizationModule),
    typeof(AbpEntityFrameworkCoreSqliteModule),
    typeof(ZahyCommissionApplicationModule),
    typeof(ZahyCommissionEntityFrameworkCoreModule)
)]
public class ZahyCommissionTestModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddAlwaysAllowAuthorization();
        context.Services.AddSingleton<CommissionTestCurrentUser>();
        context.Services.Replace(ServiceDescriptor.Singleton<ICurrentUser>(sp =>
            sp.GetRequiredService<CommissionTestCurrentUser>()));

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
        var connection = new ZahyCommissionTestSqliteConnection("Data Source=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<ZahyCommissionDbContext>()
            .UseSqlite(connection)
            .Options;

        using var context = new ZahyCommissionDbContext(options);
        context.GetService<IRelationalDatabaseCreator>().CreateTables();

        return connection;
    }
}

[DependsOn(typeof(ZahyCommissionTestModule))]
public class ZahyCommissionIntegrationTestModule : AbpModule
{
}
