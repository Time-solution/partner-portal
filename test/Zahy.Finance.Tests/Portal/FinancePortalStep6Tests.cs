using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Shouldly;
using Volo.Abp.Authorization;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Xunit;
using Zahy.Commission;

namespace Zahy.Finance;

[Collection(PdfRenderCollection.Name)]
public class FinancePortalStep6Tests : ZahyFinanceTestBase
{
    private readonly IFinancePartnerPortalAppService _partnerPortal;
    private readonly IFinanceMerchantPortalAppService _merchantPortal;
    private readonly IFinanceAccountService _accountService;
    private readonly IFinanceInvoiceGenerationService _invoiceGenerationService;
    private readonly IBillingChargeService _billingChargeService;
    private readonly ICommissionLedgerService _ledgerService;
    private readonly IGuidGenerator _guidGenerator;
    private readonly IKycFieldProtector _fieldProtector;
    private readonly TestCurrentPartner _currentPartner;
    private readonly TestCurrentTenant _currentTenant;

    public FinancePortalStep6Tests()
    {
        _partnerPortal = GetRequiredService<IFinancePartnerPortalAppService>();
        _merchantPortal = GetRequiredService<IFinanceMerchantPortalAppService>();
        _accountService = GetRequiredService<IFinanceAccountService>();
        _invoiceGenerationService = GetRequiredService<IFinanceInvoiceGenerationService>();
        _billingChargeService = GetRequiredService<IBillingChargeService>();
        _ledgerService = GetRequiredService<ICommissionLedgerService>();
        _guidGenerator = GetRequiredService<IGuidGenerator>();
        _fieldProtector = GetRequiredService<IKycFieldProtector>();
        _currentPartner = GetRequiredService<TestCurrentPartner>();
        _currentTenant = GetRequiredService<TestCurrentTenant>();
        _currentPartner.Id = null;
        _currentTenant.Id = null;
    }

    [Fact]
    public async Task Partner_Can_Download_Only_Own_Excel_Export()
    {
        var partnerA = Guid.NewGuid();
        var partnerB = Guid.NewGuid();
        await SeedPartnerWithPostingsAsync(partnerA, 20m, 0m, 0m);
        await SeedPartnerWithPostingsAsync(partnerB, 0m, 0m, 7m);

        _currentPartner.Id = partnerB;

        FinancePortalDownloadDto export = null!;
        await WithUnitOfWorkAsync(async () =>
        {
            export = await _partnerPortal.ExportPostingsAsync(new FinancePortalExportRequest
            {
                Format = FinanceExportFormat.Xlsx
            });
        });

        export.PostingSum.ShouldBe(7.00m);
        FinanceExportTotalParser.ReadExcelTotal(export.Content).ShouldBe(7.00m);
    }

    [Fact]
    public async Task Partner_Can_Download_Only_Own_Invoice_Pdf()
    {
        var partnerA = Guid.NewGuid();
        var partnerB = Guid.NewGuid();
        var documentId = await SeedPartnerInvoiceAsync(partnerA, 12m);

        _currentPartner.Id = partnerB;

        await WithUnitOfWorkAsync(async () =>
        {
            await Should.ThrowAsync<AbpAuthorizationException>(() =>
                _partnerPortal.DownloadDocumentAsync(documentId));
        });
    }

    [Fact]
    public async Task Merchant_Can_Download_Only_Own_Export()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        await SeedMerchantWithPostingsAsync(tenantA, 40m);
        await SeedMerchantWithPostingsAsync(tenantB, 11m);

        _currentTenant.Id = tenantB;

        FinancePortalDownloadDto export = null!;
        await WithUnitOfWorkAsync(async () =>
        {
            export = await _merchantPortal.ExportPostingsAsync(new FinancePortalExportRequest
            {
                Format = FinanceExportFormat.Csv
            });
        });

        export.PostingSum.ShouldBe(11.00m);
        FinanceExportTotalParser.ReadCsvTotal(export.Content).ShouldBe(11.00m);
    }

    [Fact]
    public async Task Merchant_Can_Download_Only_Own_Invoice()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var documentId = await SeedMerchantInvoiceAsync(tenantA, 18m);

        _currentTenant.Id = tenantB;

        await WithUnitOfWorkAsync(async () =>
        {
            await Should.ThrowAsync<AbpAuthorizationException>(() =>
                _merchantPortal.DownloadDocumentAsync(documentId));
        });
    }

    [Fact]
    public async Task Download_Endpoint_Rechecks_Ownership()
    {
        var partnerA = Guid.NewGuid();
        var partnerB = Guid.NewGuid();
        var documentId = await SeedPartnerInvoiceAsync(partnerA, 9m);

        _currentPartner.Id = partnerB;

        await WithUnitOfWorkAsync(async () =>
        {
            var documents = await _partnerPortal.GetDocumentsAsync();
            documents.ShouldBeEmpty();

            await Should.ThrowAsync<AbpAuthorizationException>(() =>
                _partnerPortal.DownloadDocumentAsync(documentId));
        });
    }

    [Fact]
    public void Portal_Account_Views_Are_Read_Only()
    {
        var forbiddenPrefixes = new[]
        {
            "Open", "Change", "Submit", "Verify", "Charge", "Accrue",
            "Suspend", "Close", "Insert", "Update", "Delete", "Create"
        };

        AssertReadOnlyInterface(typeof(IFinancePartnerPortalAppService), forbiddenPrefixes);
        AssertReadOnlyInterface(typeof(IFinanceMerchantPortalAppService), forbiddenPrefixes);
    }

    private static void AssertReadOnlyInterface(Type serviceType, string[] forbiddenPrefixes)
    {
        foreach (var method in serviceType.GetMethods(BindingFlags.Public | BindingFlags.Instance))
        {
            forbiddenPrefixes.Any(prefix => method.Name.StartsWith(prefix, StringComparison.Ordinal))
                .ShouldBeFalse($"Unexpected mutation method {serviceType.Name}.{method.Name}");
        }
    }

    private async Task<Guid> SeedPartnerInvoiceAsync(Guid partnerId, decimal amount)
    {
        await SeedPartnerWithPostingsAsync(partnerId, amount, 0m, 0m);
        _currentPartner.Id = partnerId;

        FinanceInvoiceGenerationResult invoice = null!;
        await WithUnitOfWorkAsync(async () =>
        {
            invoice = await _invoiceGenerationService.GeneratePartnerInvoiceOnDemandAsync(
                new FinanceInvoiceRequest
                {
                    PartnerId = partnerId,
                    IdempotencyKey = $"invoice:portal-test:{partnerId:N}"
                });
        });

        return invoice.DocumentId;
    }

    private async Task<Guid> SeedMerchantInvoiceAsync(Guid tenantId, decimal amount)
    {
        await SeedMerchantAccountOnlyAsync(tenantId);

        Guid documentId = Guid.Empty;
        await WithUnitOfWorkAsync(async () =>
        {
            var charge = await _billingChargeService.ChargeAsync(new BillingChargeRequest
            {
                PartnerId = Guid.NewGuid(),
                ChargeTarget = BillingChargeTarget.Merchant,
                TenantId = tenantId,
                Kind = BillingChargeKind.Subscription,
                Amount = amount,
                IdempotencyKey = $"billing:merchant-inv:{tenantId:N}"
            });

            var invoice = await _invoiceGenerationService.TryGenerateForBillingChargeAsync(
                new FinanceBillingInvoiceTriggerContext
                {
                    BillingChargeId = charge.ChargeId,
                    IsNew = charge.IsNew,
                    PartnerId = Guid.NewGuid(),
                    TenantId = tenantId,
                    AccountKind = FinanceAccountKind.Merchant
                });

            documentId = invoice.DocumentId;
        });

        return documentId;
    }

    private async Task SeedMerchantAccountOnlyAsync(Guid tenantId)
    {
        await WithUnitOfWorkAsync(async () =>
        {
            await SeedMerchantVerifiedKycAsync(tenantId);
            await _accountService.OpenMerchantAccountAsync(tenantId);
        });
    }

    private async Task SeedPartnerWithPostingsAsync(Guid partnerId, decimal platformAmount, decimal partnerEarnsAmount, decimal billingAmount)
    {
        await WithUnitOfWorkAsync(async () =>
        {
            await SeedPartnerVerifiedKycAsync(partnerId);
            await _accountService.OpenPartnerAccountAsync(partnerId);

            if (platformAmount > 0)
            {
                await _ledgerService.AccrueAsync(CreateAccrualRequest(partnerId, Guid.NewGuid(), 100m, platformAmount));
            }

            if (partnerEarnsAmount > 0)
            {
                await _ledgerService.AccrueAsync(CreateAccrualRequest(
                    partnerId,
                    Guid.NewGuid(),
                    50m,
                    partnerEarnsAmount,
                    CommissionDirection.PartnerEarns));
            }

            if (billingAmount > 0)
            {
                await _billingChargeService.ChargeAsync(new BillingChargeRequest
                {
                    PartnerId = partnerId,
                    ChargeTarget = BillingChargeTarget.Partner,
                    Kind = BillingChargeKind.Subscription,
                    Amount = billingAmount,
                    IdempotencyKey = $"billing:portal:{partnerId:N}:{billingAmount}"
                });
            }
        });
    }

    private async Task SeedMerchantWithPostingsAsync(Guid tenantId, decimal billingAmount)
    {
        await WithUnitOfWorkAsync(async () =>
        {
            await SeedMerchantVerifiedKycAsync(tenantId);
            await _accountService.OpenMerchantAccountAsync(tenantId);

            if (billingAmount > 0)
            {
                await _billingChargeService.ChargeAsync(new BillingChargeRequest
                {
                    PartnerId = Guid.NewGuid(),
                    ChargeTarget = BillingChargeTarget.Merchant,
                    TenantId = tenantId,
                    Kind = BillingChargeKind.Subscription,
                    Amount = billingAmount,
                    IdempotencyKey = $"billing:merchant-portal:{tenantId:N}"
                });
            }
        });
    }

    private async Task SeedPartnerVerifiedKycAsync(Guid partnerId)
    {
        var submissionRepo = GetRequiredService<IRepository<KycSubmission, Guid>>();
        var verificationRepo = GetRequiredService<IRepository<KycVerification, Guid>>();
        var submissionId = _guidGenerator.Create();
        var verificationId = _guidGenerator.Create();

        await submissionRepo.InsertAsync(
            KycSubmission.Create(
                submissionId,
                KycEntityKind.Partner,
                partnerId,
                version: 1,
                submittedByUserId: null,
                submittedAt: DateTime.UtcNow,
                new ProtectedKycFieldBundle
                {
                    LegalNameAr = _fieldProtector.Protect("Submitted AR"),
                    LegalNameEn = _fieldProtector.Protect("Submitted EN"),
                    CommercialRegistrationNumber = _fieldProtector.Protect("1010111111"),
                    VatNumber = _fieldProtector.Protect("300011111111111"),
                    Iban = _fieldProtector.Protect("SA9922334455667788990011"),
                    LegalAddress = _fieldProtector.Protect("Submitted Address")
                }),
            autoSave: true);

        var verification = KycVerification.CreateForSubmission(verificationId, KycEntityKind.Partner, partnerId, submissionId);
        verification.MarkUnderReview(Guid.NewGuid(), DateTime.UtcNow);
        verification.MarkVerified(new KycVerifiedCanonicalFields
        {
            LegalNameAr = "شركة معتمدة",
            LegalNameEn = "Verified Partner Ltd",
            CommercialRegistrationNumber = "2020222222",
            VatNumber = "300099988877766",
            Iban = "SA4420000000000012345678",
            LegalAddress = "Verified Address"
        }, Guid.NewGuid(), DateTime.UtcNow);

        await verificationRepo.InsertAsync(verification, autoSave: true);
    }

    private async Task SeedMerchantVerifiedKycAsync(Guid tenantId)
    {
        var submissionRepo = GetRequiredService<IRepository<KycSubmission, Guid>>();
        var verificationRepo = GetRequiredService<IRepository<KycVerification, Guid>>();
        var submissionId = _guidGenerator.Create();
        var verificationId = _guidGenerator.Create();

        await submissionRepo.InsertAsync(
            KycSubmission.Create(
                submissionId,
                KycEntityKind.Merchant,
                tenantId,
                version: 1,
                submittedByUserId: null,
                submittedAt: DateTime.UtcNow,
                new ProtectedKycFieldBundle
                {
                    LegalNameAr = _fieldProtector.Protect("تاجر"),
                    LegalNameEn = _fieldProtector.Protect("Merchant Submitted"),
                    CommercialRegistrationNumber = _fieldProtector.Protect("3030333333"),
                    VatNumber = _fieldProtector.Protect("300033333333333"),
                    Iban = _fieldProtector.Protect("SA9922334455667788990011"),
                    LegalAddress = _fieldProtector.Protect("Merchant Submitted Address")
                }),
            autoSave: true);

        var verification = KycVerification.CreateForSubmission(verificationId, KycEntityKind.Merchant, tenantId, submissionId);
        verification.MarkUnderReview(Guid.NewGuid(), DateTime.UtcNow);
        verification.MarkVerified(new KycVerifiedCanonicalFields
        {
            LegalNameAr = "تاجر معتمد",
            LegalNameEn = "Verified Merchant Ltd",
            CommercialRegistrationNumber = "4040444444",
            VatNumber = "300044444444444",
            Iban = "SA4420000000000012345678",
            LegalAddress = "Verified Merchant Address"
        }, Guid.NewGuid(), DateTime.UtcNow);

        await verificationRepo.InsertAsync(verification, autoSave: true);
    }

    private static CommissionAccrualRequest CreateAccrualRequest(
        Guid partnerId,
        Guid ruleId,
        decimal basisAmount,
        decimal computedCommission,
        CommissionDirection direction = CommissionDirection.PlatformEarns) =>
        new()
        {
            PartnerId = partnerId,
            TenantId = Guid.NewGuid(),
            RuleId = ruleId,
            SourceType = "order.paid",
            SourceId = Guid.NewGuid().ToString("N"),
            OrderRecordId = Guid.NewGuid(),
            BasisAmount = basisAmount,
            ComputedCommission = computedCommission,
            Direction = direction,
            Currency = CommissionConsts.DefaultCurrency
        };
}
