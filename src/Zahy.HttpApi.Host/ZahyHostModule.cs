using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.CookiePolicy;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using OpenIddict.Server;
using OpenIddict.Validation.AspNetCore;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.Autofac;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.Data;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore.SqlServer;
using Volo.Abp.Identity.AspNetCore;
using Volo.Abp.Modularity;
using Volo.Abp.OpenIddict;
using Volo.Abp.Swashbuckle;
using Volo.Abp.Uow;
using Zahy.Hosting;
using Zahy.Identity;
using Zahy.PartnerPlatform;
using Zahy.Webhooks;
using Zahy.OrderLedger;
using Zahy.Commission;
using Zahy.Connectors;
using Zahy.Finance;
using Zahy.PartnerCatalog;
using Zahy.Settlement;

namespace Zahy;

[DependsOn(
    typeof(AbpAutofacModule),
    typeof(AbpBackgroundWorkersModule),
    typeof(AbpAspNetCoreMvcModule),
    typeof(AbpSwashbuckleModule),
    typeof(AbpEntityFrameworkCoreSqlServerModule),
    typeof(AbpOpenIddictAspNetCoreModule),
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
    typeof(ZahyOrderLedgerWebhooksModule),
    typeof(ZahyOrderLedgerEntityFrameworkCoreModule),
    typeof(ZahyOrderLedgerHttpApiModule),
    typeof(ZahyConnectorsOrderLedgerModule),
    typeof(ZahyConnectorsEntityFrameworkCoreModule),
    typeof(ZahyCommissionApplicationModule),
    typeof(ZahyCommissionOrderLedgerModule),
    typeof(ZahyCommissionEntityFrameworkCoreModule),
    typeof(ZahyCommissionHttpApiModule),
    typeof(ZahyFinanceApplicationModule),
    typeof(ZahyFinanceCommissionModule),
    typeof(ZahyFinanceEntityFrameworkCoreModule),
    typeof(ZahyFinanceHttpApiModule),
    typeof(ZahyPartnerCatalogApplicationModule),
    typeof(ZahyPartnerCatalogEntityFrameworkCoreModule),
    typeof(ZahyPartnerCatalogHttpApiModule),
    typeof(ZahySettlementApplicationModule),
    typeof(ZahySettlementEntityFrameworkCoreModule),
    typeof(ZahySettlementHttpApiModule)
)]
public class ZahyHostModule : AbpModule
{
    public override void PreConfigureServices(ServiceConfigurationContext context)
    {
        var hostingEnvironment = context.Services.GetHostingEnvironment();
        var configuration = context.Services.GetConfiguration();

        OpenIddictHostStartupGuard.EnsureProductionCertificateConfigured(hostingEnvironment, configuration);

        PreConfigure<OpenIddictServerBuilder>(serverBuilder =>
        {
            // Token policy: short-lived access tokens (~30 min) + 14-day refresh.
            // OpenIddict 6 uses rolling (rotating, one-time-use) refresh tokens by
            // default — each refresh marks the old token redeemed, issues a new one,
            // and detects reuse (30s concurrency leeway). No opt-in call required.
            serverBuilder.SetAccessTokenLifetime(TimeSpan.FromMinutes(30));
            serverBuilder.SetRefreshTokenLifetime(TimeSpan.FromDays(14));
        });

        // Same host acts as IdP + resource server; validation uses the local server.
        PreConfigure<OpenIddictBuilder>(builder =>
        {
            builder.AddValidation(options =>
            {
                options.UseLocalServer();
                options.UseAspNetCore();
            });
        });

        // Signing/encryption keys: dev uses an auto-generated (gitignored) cert;
        // production keys come from the secret store / config — never committed.
        if (!hostingEnvironment.IsDevelopment())
        {
            PreConfigure<AbpOpenIddictAspNetCoreOptions>(options =>
            {
                options.AddDevelopmentEncryptionAndSigningCertificate = false;
            });

            var certPath = configuration["OpenIddict:Certificate:Path"]!;
            var certPassword = configuration["OpenIddict:Certificate:Password"];
            PreConfigure<OpenIddictServerBuilder>(serverBuilder =>
            {
                serverBuilder.AddProductionEncryptionAndSigningCertificate(certPath, certPassword!);
            });
        }
    }

    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var configuration = context.Services.GetConfiguration();
        var hostingEnvironment = context.Services.GetHostingEnvironment();

        ZahyDataProtectionConfigurator.Configure(context.Services, configuration, hostingEnvironment);

        Configure<AbpDbContextOptions>(options =>
        {
            options.UseSqlServer();
        });

        Configure<AbpDbConnectionOptions>(options =>
        {
            options.ConnectionStrings.Default = configuration.GetConnectionString("Default");
        });

        context.Services.Replace(ServiceDescriptor.Transient<IFinanceInvoiceNumberAllocator, SqlServerFinanceInvoiceNumberAllocatorAdapter>());

        // API-only host: disable background workers (OpenIddict token cleanup NRE without full infra).
        Configure<AbpBackgroundWorkerOptions>(options =>
        {
            options.IsEnabled = false;
        });

        // Cross-origin SPA (http://localhost:5173) → API cookie (https://localhost:44300).
        context.Services.Configure<CookiePolicyOptions>(options =>
        {
            options.MinimumSameSitePolicy = SameSiteMode.None;
            options.Secure = CookieSecurePolicy.Always;
        });

        context.Services.ConfigureApplicationCookie(options =>
        {
            options.Cookie.SameSite = SameSiteMode.None;
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
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

        context.Services.AddAbpSwaggerGen(options =>
        {
            options.DocInclusionPredicate((_, _) => true);
            options.CustomSchemaIds(type => type.FullName);
        });
    }

    public override void OnApplicationInitialization(ApplicationInitializationContext context)
    {
        var app = context.GetApplicationBuilder();
        var env = context.GetEnvironment();

        app.UseRouting();
        app.UseCookiePolicy();
        app.UseCors();
        app.UseAuthentication();
        app.UseAbpOpenIddictValidation();
        app.UseUnitOfWork();
        app.UseAuthorization();

        if (env.IsDevelopment())
        {
            app.UseSwagger();
            app.UseAbpSwaggerUI(options =>
            {
                options.SwaggerEndpoint("/swagger/v1/swagger.json", "Zahy Partner Platform API");
            });
        }

        app.UseConfiguredEndpoints(endpoints =>
        {
            if (env.IsDevelopment())
            {
                endpoints.MapGet("/", () => Results.Redirect("/swagger/index.html"));
            }
        });
    }
}
