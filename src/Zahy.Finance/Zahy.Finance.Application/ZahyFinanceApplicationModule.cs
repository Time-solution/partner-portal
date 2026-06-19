using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.Application;
using Volo.Abp.Modularity;

namespace Zahy.Finance;

[DependsOn(
    typeof(ZahyFinanceDomainModule),
    typeof(ZahyFinanceApplicationContractsModule),
    typeof(AbpDddApplicationModule)
)]
public class ZahyFinanceApplicationModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddTransient<IFinanceAccountService, FinanceAccountService>();
        context.Services.AddTransient<IFinanceAccountQueryService, FinanceAccountService>();
        context.Services.AddTransient<FinancePostingIngestionService>();
        context.Services.AddTransient<FinanceAccessGuard>();
        context.Services.AddTransient<IKycFieldProtector, DevAppLayerKycFieldProtector>();
        context.Services.AddTransient<IKycSubmissionService, KycSubmissionService>();
        context.Services.AddTransient<IKycVerificationService, KycVerificationService>();
        context.Services.AddTransient<IKycCanonicalProfileProvider, KycCanonicalProfileProvider>();
        context.Services.AddTransient<IFinanceDocumentKycBlockBuilder, FinanceDocumentKycBlockBuilder>();
        context.Services.Configure<FinanceKycProtectionOptions>(_ => { });
    }
}
