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
using Zahy.Identity.Auditing;
using Zahy.Identity.OpenIddict;
using Zahy.Identity.Partners;
using Zahy.PartnerPlatform.Partners;

namespace Zahy.PartnerPlatform;

[DependsOn(
    typeof(AbpAutofacModule),
    typeof(AbpTestBaseModule),
    typeof(AbpAuthorizationModule),
    typeof(AbpEntityFrameworkCoreSqliteModule),
    typeof(ZahyPartnerPlatformApplicationModule),
    typeof(ZahyPartnerPlatformEntityFrameworkCoreModule)
)]
public class ZahyPartnerPlatformTestModule : AbpModule
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
        var connection = new ZahyPartnerPlatformTestSqliteConnection("Data Source=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<ZahyPartnerPlatformDbContext>()
            .UseSqlite(connection)
            .Options;

        using (var context = new ZahyPartnerPlatformDbContext(options))
        {
            context.GetService<IRelationalDatabaseCreator>().CreateTables();
        }

        return connection;
    }
}

[DependsOn(typeof(ZahyPartnerPlatformTestModule))]
public class ZahyPartnerPlatformIntegrationTestModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddAlwaysAllowAuthorization();
        context.Services.Replace(ServiceDescriptor.Singleton<IAdminAuditLogger, RecordingAdminAuditLogger>());
        context.Services.Replace(ServiceDescriptor.Singleton<IPartnerM2MClientProvisioner, FakePartnerM2MClientProvisioner>());
        context.Services.Replace(ServiceDescriptor.Singleton<IPartnerUserInviteService, FakePartnerUserInviteService>());
        context.Services.Replace(ServiceDescriptor.Singleton<ICurrentPartner, TestCurrentPartnerAccessor>());
        context.Services.AddSingleton<TestCurrentPartner>();
    }
}
