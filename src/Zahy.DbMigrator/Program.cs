using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Volo.Abp;
using Volo.Abp.Autofac;
using Volo.Abp.Data;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore.SqlServer;
using Volo.Abp.Modularity;
using Zahy.Identity;
using Zahy.Identity.EntityFrameworkCore;
using Zahy.PartnerPlatform;
using Zahy.PartnerPlatform.EntityFrameworkCore;

namespace Zahy.DbMigrator;

[DependsOn(
    typeof(AbpAutofacModule),
    typeof(AbpEntityFrameworkCoreSqlServerModule),
    typeof(ZahyIdentityEntityFrameworkCoreModule),
    typeof(ZahyIdentityApplicationContractsModule),
    typeof(ZahyPartnerPlatformEntityFrameworkCoreModule),
    typeof(ZahyPartnerPlatformApplicationContractsModule)
)]
public class DbMigratorModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var configuration = context.Services.GetConfiguration();

        Configure<AbpDbContextOptions>(options =>
        {
            options.UseSqlServer();
        });

        Configure<AbpDbConnectionOptions>(options =>
        {
            options.ConnectionStrings.Default = configuration.GetConnectionString("Default");
        });
    }
}

public class DbMigratorHostedService : IHostedService
{
    private readonly IHostApplicationLifetime _hostApplicationLifetime;
    private readonly IConfiguration _configuration;

    public DbMigratorHostedService(
        IHostApplicationLifetime hostApplicationLifetime,
        IConfiguration configuration)
    {
        _hostApplicationLifetime = hostApplicationLifetime;
        _configuration = configuration;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var application = await AbpApplicationFactory.CreateAsync<DbMigratorModule>(options =>
        {
            options.Services.ReplaceConfiguration(_configuration);
            options.UseAutofac();
        });

        await application.InitializeAsync();

        var identityMigrator = application.ServiceProvider.GetRequiredService<IZahyIdentityDbSchemaMigrator>();
        await identityMigrator.MigrateAsync();
        Console.WriteLine("Identity migrations applied.");

        var partnerMigrator =
            application.ServiceProvider.GetRequiredService<IZahyPartnerPlatformDbSchemaMigrator>();
        await partnerMigrator.MigrateAsync();
        Console.WriteLine("Partner Platform migrations applied.");

        await application.ServiceProvider.GetRequiredService<IDataSeeder>().SeedAsync();
        Console.WriteLine("Standard data seed completed (includes dev test users when Development + Zahy:DevSeed:Enabled=true).");

        await application.ShutdownAsync();
        _hostApplicationLifetime.StopApplication();
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

public static class Program
{
    public static async Task Main(string[] args)
    {
        await Host.CreateDefaultBuilder(args)
            .ConfigureServices(services =>
            {
                services.AddHostedService<DbMigratorHostedService>();
            })
            .RunConsoleAsync();
    }
}
