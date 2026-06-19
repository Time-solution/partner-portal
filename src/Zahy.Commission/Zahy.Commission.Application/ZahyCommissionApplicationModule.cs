using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.Application;
using Volo.Abp.Modularity;

namespace Zahy.Commission;

[DependsOn(
    typeof(ZahyCommissionDomainModule),
    typeof(ZahyCommissionApplicationContractsModule),
    typeof(AbpDddApplicationModule)
)]
public class ZahyCommissionApplicationModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddTransient<ICommissionCalculator, CommissionCalculator>();
        context.Services.AddTransient<ICommissionBasisAmountResolver, CommissionBasisAmountResolver>();
        context.Services.AddTransient<ICommissionRuleWinnerResolver, CommissionRuleWinnerResolver>();
        context.Services.AddTransient<ICommissionLedgerService, CommissionLedgerService>();
        context.Services.AddTransient<ICommissionAccrualService, CommissionAccrualService>();
        context.Services.AddTransient<IBillingChargeService, BillingChargeService>();
        context.Services.AddTransient<ICommissionPartnerTypeLookup, NullCommissionPartnerTypeLookup>();
        context.Services.AddTransient<ICommissionLedgerFinanceTrigger, NullCommissionLedgerFinanceTrigger>();
        context.Services.AddTransient<IBillingChargeFinanceTrigger, NullBillingChargeFinanceTrigger>();
    }
}