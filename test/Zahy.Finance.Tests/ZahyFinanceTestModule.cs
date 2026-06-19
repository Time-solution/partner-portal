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
using Zahy.Commission;
using Zahy.Identity.Partners;

namespace Zahy.Finance;

[DependsOn(
    typeof(AbpAutofacModule),
    typeof(AbpTestBaseModule),
    typeof(AbpAuthorizationModule),
    typeof(AbpEntityFrameworkCoreSqliteModule),
    typeof(ZahyFinanceApplicationModule),
    typeof(ZahyFinanceEntityFrameworkCoreModule),
    typeof(ZahyFinanceCommissionModule),
    typeof(ZahyCommissionEntityFrameworkCoreModule)
)]
public class ZahyFinanceTestModule : AbpModule
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
        var connection = new ZahyFinanceTestSqliteConnection("Data Source=:memory:");
        connection.Open();

        var financeOptions = new DbContextOptionsBuilder<ZahyFinanceDbContext>()
            .UseSqlite(connection)
            .Options;

        using (var financeContext = new ZahyFinanceDbContext(financeOptions))
        {
            financeContext.GetService<IRelationalDatabaseCreator>().CreateTables();
        }

        var commissionOptions = new DbContextOptionsBuilder<ZahyCommissionDbContext>()
            .UseSqlite(connection)
            .Options;

        using (var commissionContext = new ZahyCommissionDbContext(commissionOptions))
        {
            commissionContext.GetService<IRelationalDatabaseCreator>().CreateTables();
        }

        return connection;
    }
}

[DependsOn(typeof(ZahyFinanceTestModule))]
public class ZahyFinanceIntegrationTestModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.Replace(ServiceDescriptor.Singleton<ICurrentPartner, TestCurrentPartnerAccessor>());
        context.Services.AddSingleton<TestCurrentPartner>();
    }
}
