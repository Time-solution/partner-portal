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
using Zahy.Identity.Partners;

namespace Zahy.PartnerCatalog;

[DependsOn(
    typeof(AbpAutofacModule),
    typeof(AbpTestBaseModule),
    typeof(AbpAuthorizationModule),
    typeof(AbpEntityFrameworkCoreSqliteModule),
    typeof(ZahyPartnerCatalogEntityFrameworkCoreModule),
    typeof(ZahyPartnerCatalogApplicationModule)
)]
public class ZahyPartnerCatalogTestModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddAlwaysAllowAuthorization();
        context.Services.AddSingleton<PartnerCatalogTestCurrentPartner>();
        context.Services.Replace(ServiceDescriptor.Singleton<ICurrentPartner>(sp =>
            sp.GetRequiredService<PartnerCatalogTestCurrentPartner>()));
        context.Services.AddSingleton<Volo.Abp.Authorization.Permissions.IPermissionChecker, PartnerCatalogAllowAllPermissionChecker>();

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
        var connection = new PartnerCatalogTestSqliteConnection("Data Source=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<ZahyPartnerCatalogDbContext>()
            .UseSqlite(connection)
            .Options;

        using var context = new ZahyPartnerCatalogDbContext(options);
        context.GetService<IRelationalDatabaseCreator>().CreateTables();

        return connection;
    }
}

public abstract class ZahyPartnerCatalogTestBase : AbpIntegratedTest<ZahyPartnerCatalogTestModule>
{
    protected override void SetAbpApplicationCreationOptions(AbpApplicationCreationOptions options)
    {
        options.UseAutofac();
    }

    protected virtual async Task WithUnitOfWorkAsync(Func<Task> action)
    {
        var uowManager = GetRequiredService<Volo.Abp.Uow.IUnitOfWorkManager>();
        using var uow = uowManager.Begin(new Volo.Abp.Uow.AbpUnitOfWorkOptions(), requiresNew: true);
        await action();
        await uow.CompleteAsync();
    }
}

internal sealed class PartnerCatalogTestSqliteConnection : SqliteConnection
{
    public PartnerCatalogTestSqliteConnection(string connectionString)
        : base(connectionString)
    {
    }
}

internal sealed class PartnerCatalogAllowAllPermissionChecker : Volo.Abp.Authorization.Permissions.IPermissionChecker
{
    public Task<bool> IsGrantedAsync(string name) => Task.FromResult(true);

    public Task<bool> IsGrantedAsync(System.Security.Claims.ClaimsPrincipal? claimsPrincipal, string name) =>
        Task.FromResult(true);

    public Task<Volo.Abp.Authorization.Permissions.MultiplePermissionGrantResult> IsGrantedAsync(string[] names)
    {
        var result = new Volo.Abp.Authorization.Permissions.MultiplePermissionGrantResult();
        foreach (var name in names)
        {
            result.Result[name] = Volo.Abp.Authorization.Permissions.PermissionGrantResult.Granted;
        }

        return Task.FromResult(result);
    }

    public Task<Volo.Abp.Authorization.Permissions.MultiplePermissionGrantResult> IsGrantedAsync(
        System.Security.Claims.ClaimsPrincipal? claimsPrincipal,
        string[] names) => IsGrantedAsync(names);
}
