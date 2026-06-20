using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.Application;
using Volo.Abp.Modularity;
using Zahy.Commission;
using Zahy.Settlement;

namespace Zahy.PartnerCatalog;

[DependsOn(
    typeof(ZahyPartnerCatalogDomainModule),
    typeof(ZahyPartnerCatalogApplicationContractsModule),
    typeof(ZahySettlementApplicationModule),
    typeof(ZahyCommissionApplicationModule),
    typeof(AbpDddApplicationModule)
)]
public class ZahyPartnerCatalogApplicationModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var configuration = context.Services.GetConfiguration();

        Configure<PartnerCatalogMerchantOptions>(
            configuration.GetSection(PartnerCatalogMerchantOptions.SectionName));

        Configure<ConsignmentSettlementOptions>(
            configuration.GetSection(ConsignmentSettlementOptions.SectionName));

        context.Services.AddTransient<ISettlementCatalogBridge, SettlementCatalogBridge>();
        context.Services.AddTransient<IPartnerCatalogParticipationBridge, PartnerCatalogParticipationBridge>();
        context.Services.AddTransient<IConsignmentSaleSettlementRouter, ConsignmentSaleSettlementRouter>();
        context.Services.AddTransient<ReflectionOnlyOrderBridge>();
        context.Services.AddTransient<SubscriptionFeeBillingBridge>();
    }
}
