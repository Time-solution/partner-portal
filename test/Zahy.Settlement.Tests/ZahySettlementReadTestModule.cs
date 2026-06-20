using System;
using System.Threading.Tasks;
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
using Volo.Abp.Uow;
using Zahy.Commission;
using Zahy.Identity.Partners;
using Zahy.PartnerCatalog;

namespace Zahy.Settlement.Read;

[DependsOn(
    typeof(AbpAutofacModule),
    typeof(AbpTestBaseModule),
    typeof(AbpAuthorizationModule),
    typeof(AbpEntityFrameworkCoreSqliteModule),
    typeof(ZahySettlementApplicationModule),
    typeof(ZahySettlementEntityFrameworkCoreModule),
    typeof(ZahyPartnerCatalogEntityFrameworkCoreModule),
    typeof(ZahyCommissionEntityFrameworkCoreModule)
)]
public class ZahySettlementReadTestModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddAlwaysAllowAuthorization();
        context.Services.AddSingleton<SettlementReadTestCurrentPartner>();
        context.Services.Replace(ServiceDescriptor.Singleton<ICurrentPartner>(sp =>
            sp.GetRequiredService<SettlementReadTestCurrentPartner>()));

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
        var connection = new SettlementReadTestSqliteConnection("Data Source=:memory:");
        connection.Open();

        CreateTables<ZahySettlementDbContext>(connection);
        CreateTables<ZahyPartnerCatalogDbContext>(connection);
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

public abstract class ZahySettlementReadTestBase : AbpIntegratedTest<ZahySettlementReadTestModule>
{
    protected override void SetAbpApplicationCreationOptions(AbpApplicationCreationOptions options)
    {
        options.UseAutofac();
    }

    protected virtual async Task WithUnitOfWorkAsync(Func<Task> action)
    {
        var uowManager = GetRequiredService<IUnitOfWorkManager>();
        using var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true);
        await action();
        await uow.CompleteAsync();
    }
}

public sealed class SettlementReadTestCurrentPartner : ICurrentPartner
{
    public Guid? Id { get; set; }
}

internal sealed class SettlementReadTestSqliteConnection : SqliteConnection
{
    public SettlementReadTestSqliteConnection(string connectionString)
        : base(connectionString)
    {
    }
}
