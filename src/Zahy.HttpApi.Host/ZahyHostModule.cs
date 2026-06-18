using Microsoft.AspNetCore.Builder;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.Autofac;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore.SqlServer;
using Volo.Abp.Modularity;
using Zahy.Identity;
using Zahy.PartnerPlatform;
using Zahy.Webhooks;
using Zahy.OrderLedger;
using Zahy.Commission;

namespace Zahy;

[DependsOn(
    typeof(AbpAutofacModule),
    typeof(AbpAspNetCoreMvcModule),
    typeof(AbpEntityFrameworkCoreSqlServerModule),
    typeof(ZahyIdentityApplicationModule),
    typeof(ZahyIdentityEntityFrameworkCoreModule),
    typeof(ZahyIdentityHttpApiModule),
    typeof(ZahyPartnerPlatformApplicationModule),
    typeof(ZahyPartnerPlatformEntityFrameworkCoreModule),
    typeof(ZahyPartnerPlatformHttpApiModule),
    typeof(ZahyWebhooksApplicationModule),
    typeof(ZahyWebhooksEntityFrameworkCoreModule),
    typeof(ZahyWebhooksHttpApiModule),
    typeof(ZahyOrderLedgerApplicationModule),
    typeof(ZahyOrderLedgerEntityFrameworkCoreModule),
    typeof(ZahyOrderLedgerHttpApiModule),
    typeof(ZahyCommissionApplicationModule),
    typeof(ZahyCommissionEntityFrameworkCoreModule),
    typeof(ZahyCommissionHttpApiModule)
)]
public class ZahyHostModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        Configure<AbpDbContextOptions>(options =>
        {
            options.UseSqlServer();
        });
    }

    public override void OnApplicationInitialization(ApplicationInitializationContext context)
    {
        var app = context.GetApplicationBuilder();
        app.UseRouting();
        app.UseConfiguredEndpoints();
    }
}