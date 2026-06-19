using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.Application;
using Volo.Abp.Auditing;
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
        Configure<AbpAuditingOptions>(options =>
        {
            options.IgnoredTypes.Add(typeof(KycSubmissionRequest));
            options.IgnoredTypes.Add(typeof(KycVerifyRequest));
            options.IgnoredTypes.Add(typeof(KycRejectRequest));
        });

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
        context.Services.AddTransient<IFinancePostingReadService, FinancePostingReadService>();
        context.Services.AddTransient<IFinanceDocumentExportService, FinanceDocumentExportService>();
        context.Services.AddTransient<IFinanceStatementDocumentService, FinanceStatementDocumentService>();
        context.Services.AddTransient<IFinanceInvoiceDocumentService, FinanceInvoiceDocumentService>();
        context.Services.AddTransient<IZatcaInvoiceShaper, LocalZatcaInvoiceShaper>();
        context.Services.AddTransient<IZatcaSubmitter, NullZatcaSubmitter>();
        context.Services.Configure<FinanceBrandingOptions>(_ => { });
    }
}
