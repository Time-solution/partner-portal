using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenIddict.Server;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.Autofac;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore.SqlServer;
using Volo.Abp.Identity.AspNetCore;
using Volo.Abp.Modularity;
using Volo.Abp.OpenIddict;
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
    typeof(AbpOpenIddictAspNetCoreModule),
    typeof(AbpIdentityAspNetCoreModule),
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
    public override void PreConfigureServices(ServiceConfigurationContext context)
    {
        var hostingEnvironment = context.Services.GetHostingEnvironment();
        var configuration = context.Services.GetConfiguration();

        PreConfigure<OpenIddictServerBuilder>(serverBuilder =>
        {
            // Token policy: short-lived access tokens (~30 min) + 14-day refresh.
            // OpenIddict 6 uses rolling (rotating, one-time-use) refresh tokens by
            // default — each refresh marks the old token redeemed, issues a new one,
            // and detects reuse (30s concurrency leeway). No opt-in call required.
            serverBuilder.SetAccessTokenLifetime(TimeSpan.FromMinutes(30));
            serverBuilder.SetRefreshTokenLifetime(TimeSpan.FromDays(14));
        });

        // Signing/encryption keys: dev uses an auto-generated (gitignored) cert;
        // production keys come from the secret store / config — never committed.
        if (!hostingEnvironment.IsDevelopment())
        {
            PreConfigure<AbpOpenIddictAspNetCoreOptions>(options =>
            {
                options.AddDevelopmentEncryptionAndSigningCertificate = false;
            });

            var certPath = configuration["OpenIddict:Certificate:Path"];
            var certPassword = configuration["OpenIddict:Certificate:Password"];
            if (!string.IsNullOrWhiteSpace(certPath))
            {
                PreConfigure<OpenIddictServerBuilder>(serverBuilder =>
                {
                    serverBuilder.AddProductionEncryptionAndSigningCertificate(certPath, certPassword!);
                });
            }
        }
    }

    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var configuration = context.Services.GetConfiguration();

        Configure<AbpDbContextOptions>(options =>
        {
            options.UseSqlServer();
        });

        var corsOrigins = (configuration["App:CorsOrigins"] ?? "http://localhost:5173")
            .Split(",", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        context.Services.AddCors(options =>
        {
            options.AddDefaultPolicy(builder =>
            {
                builder
                    .WithOrigins(corsOrigins)
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    .AllowCredentials();
            });
        });
    }

    public override void OnApplicationInitialization(ApplicationInitializationContext context)
    {
        var app = context.GetApplicationBuilder();

        app.UseRouting();
        app.UseCors();
        app.UseAuthentication();
        app.UseAbpOpenIddictValidation();
        app.UseAuthorization();
        app.UseConfiguredEndpoints();
    }
}
