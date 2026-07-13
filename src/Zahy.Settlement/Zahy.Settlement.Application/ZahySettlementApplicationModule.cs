using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Volo.Abp;
using Volo.Abp.Application;
using Volo.Abp.Modularity;

namespace Zahy.Settlement;

[DependsOn(
    typeof(ZahySettlementDomainModule),
    typeof(ZahySettlementApplicationContractsModule),
    typeof(AbpDddApplicationModule)
)]
public class ZahySettlementApplicationModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var configuration = context.Services.GetConfiguration();

        // VAT rate + per-book agent/principal treatment come from config — never hardcoded.
        Configure<SettlementVatOptions>(configuration.GetSection(SettlementVatOptions.SectionName));
        // Engine flags — disbursement + live provider OFF until sign-off (computation-only bridge in 2b).
        Configure<SettlementEngineOptions>(configuration.GetSection(SettlementEngineOptions.SectionName));
        // Inbound webhook security — replay window + server-side signing-secret source (never the caller).
        Configure<SettlementWebhookSecurityOptions>(configuration.GetSection(SettlementWebhookSecurityOptions.SectionName));

        // Composition over inheritance: each per-partner-type flow profile is registered as a
        // strategy; the resolver receives them all and routes by book (DESIGN.md §6.1).
        context.Services.AddTransient<ISettlementFlowProfile, AggregatorFlowProfile>();
        context.Services.AddTransient<ISettlementFlowProfile, ServiceFlowProfile>();
        context.Services.AddTransient<ISettlementFlowProfileResolver, SettlementFlowProfileResolver>();
        context.Services.AddTransient<IResaleVatCalculator, ResaleVatCalculator>();
        context.Services.AddTransient<ISettlementResaleTriggerPort, SettlementResaleTriggerService>();

        // Gate 1 — aggregator statement reconciliation (compute-only; no posting, no money movement).
        // The reflected-order source stays the mock/in-memory default until the live wiring gate.
        context.Services.AddTransient<AggregatorReconciliationService>();
        context.Services.AddSingleton<InMemoryAggregatorReflectedOrderSource>();
        context.Services.TryAddSingleton<IAggregatorReflectedOrderSource>(sp =>
            sp.GetRequiredService<InMemoryAggregatorReflectedOrderSource>());

        // P4 — webhook ingestion + allocation + explain.
        context.Services.AddTransient<ISigningSecretResolver, ConfigSigningSecretResolver>();
        context.Services.AddTransient<ISettlementWebhookSignatureVerifier, HmacSettlementWebhookSignatureVerifier>();
        context.Services.AddTransient<ISettlementAllocator, SettlementAllocator>();
        context.Services.AddTransient<ISettlementWebhookIngestionService, SettlementWebhookIngestionService>();
        context.Services.AddTransient<ISettlementExplainService, SettlementExplainService>();
    }
}
