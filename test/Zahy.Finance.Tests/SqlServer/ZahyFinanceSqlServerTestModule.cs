using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Volo.Abp;
using Volo.Abp.Autofac;
using Volo.Abp.Data;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore.SqlServer;
using Volo.Abp.Modularity;
using Volo.Abp.Testing;

namespace Zahy.Finance;

[DependsOn(
    typeof(AbpAutofacModule),
    typeof(AbpTestBaseModule),
    typeof(AbpEntityFrameworkCoreSqlServerModule),
    typeof(ZahyFinanceApplicationModule),
    typeof(ZahyFinanceEntityFrameworkCoreModule),
    typeof(ZahyFinanceIntegrationTestModule)
)]
public class ZahyFinanceSqlServerTestModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        Configure<AbpDbContextOptions>(options =>
        {
            options.UseSqlServer();
        });

        Configure<AbpDbConnectionOptions>(options =>
        {
            options.ConnectionStrings.Default = SqlServerTestEnvironment.ConnectionString;
        });

        context.Services.Replace(ServiceDescriptor.Transient<IFinanceInvoiceNumberAllocator, SqlServerFinanceInvoiceNumberAllocatorAdapter>());
    }
}
