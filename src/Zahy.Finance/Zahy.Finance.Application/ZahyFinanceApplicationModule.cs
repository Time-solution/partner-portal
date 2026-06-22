using Microsoft.Extensions.Configuration;
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
        // VAT rate is config-driven (default 0.15) and applied as a VAT-INCLUSIVE back-out — the SAME
        // convention as the Settlement engine, so Finance + Settlement split an identical amount identically.
        context.Services.Configure<FinanceVatOptions>(
            context.Services.GetConfiguration().GetSection("Finance:Vat"));
        context.Services.AddTransient<FinancePlatformSettingsProvider>();
        context.Services.AddTransient<IFinanceInvoiceNumberAllocator, FinanceInvoiceNumberAllocator>();
        context.Services.AddTransient<DefaultFinanceInvoicePdfGenerator>();
        context.Services.AddTransient<IFinanceInvoicePdfGenerator, DefaultFinanceInvoicePdfGenerator>();
        context.Services.AddTransient<IFinanceInvoiceGenerationService, FinanceInvoiceGenerationService>();
        context.Services.AddTransient<IFinanceDocumentAppService, FinanceManualInvoiceAppService>();
        context.Services.AddTransient<IInvoiceTrigger, BillingChargeInvoiceTrigger>();
        context.Services.AddTransient<IFinanceAccountStatusService, FinanceAccountStatusService>();
        context.Services.AddTransient<IMerchantOperationalStatusService, MerchantOperationalStatusService>();
        context.Services.AddTransient<FinancePortalReadService>();
        context.Services.AddTransient<FinanceDocumentDownloadService>();
        context.Services.AddTransient<IFinancePartnerPortalAppService, FinancePartnerPortalAppService>();
        context.Services.AddTransient<IFinanceMerchantPortalAppService, FinanceMerchantPortalAppService>();
        // Per-transaction VAT export (Accountant / PlatformAdmin only). The journal source is a seam —
        // the default returns nothing because no posted VAT-coded GL exists yet (compute-only, flag OFF).
        context.Services.AddTransient<IFinanceVatJournalProvider, NullFinanceVatJournalProvider>();
        context.Services.AddTransient<IFinanceVatExportAppService, FinanceVatExportAppService>();
    }
}
