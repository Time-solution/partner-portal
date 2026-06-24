using System;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using Volo.Abp.Authorization;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Xunit;
using Zahy.Commission;

namespace Zahy.Finance;

[Collection(PdfRenderCollection.Name)]
public class FinanceDocumentStep4Tests : ZahyFinanceTestBase
{
    private readonly IFinanceAccountService _accountService;
    private readonly IFinanceAccountQueryService _accountQueryService;
    private readonly IFinanceDocumentExportService _exportService;
    private readonly IFinanceStatementDocumentService _statementService;
    private readonly IFinanceInvoiceDocumentService _invoiceService;
    private readonly ICommissionLedgerService _ledgerService;
    private readonly IBillingChargeService _billingChargeService;
    private readonly IGuidGenerator _guidGenerator;
    private readonly IKycFieldProtector _fieldProtector;
    private readonly TestCurrentPartner _currentPartner;
    private readonly TestCurrentTenant _currentTenant;

    public FinanceDocumentStep4Tests()
    {
        _accountService = GetRequiredService<IFinanceAccountService>();
        _accountQueryService = GetRequiredService<IFinanceAccountQueryService>();
        _exportService = GetRequiredService<IFinanceDocumentExportService>();
        _statementService = GetRequiredService<IFinanceStatementDocumentService>();
        _invoiceService = GetRequiredService<IFinanceInvoiceDocumentService>();
        _ledgerService = GetRequiredService<ICommissionLedgerService>();
        _billingChargeService = GetRequiredService<IBillingChargeService>();
        _guidGenerator = GetRequiredService<IGuidGenerator>();
        _fieldProtector = GetRequiredService<IKycFieldProtector>();
        _currentPartner = GetRequiredService<TestCurrentPartner>();
        _currentTenant = GetRequiredService<TestCurrentTenant>();
        _currentPartner.Id = null;
        _currentTenant.Id = null;
    }

    [Fact]
    public async Task Export_Totals_Match_Ledger()
    {
        var partnerId = Guid.NewGuid();
        await SeedPartnerWithPostingsAsync(partnerId, platformAmount: 10m, partnerEarnsAmount: 5m, billingAmount: 20m);
        _currentPartner.Id = partnerId;

        FinanceAccountBalanceDto balance = null!;
        FinanceDocumentBytesResult csv = null!;
        FinanceDocumentBytesResult xlsx = null!;
        FinanceDocumentBytesResult statement = null!;

        await WithUnitOfWorkAsync(async () =>
        {
            balance = await _accountQueryService.GetPartnerBalanceAsync(partnerId);
            csv = await _exportService.ExportPartnerPostingsAsync(new FinanceDocumentExportRequest
            {
                EntityId = partnerId,
                Format = FinanceExportFormat.Csv
            });
            xlsx = await _exportService.ExportPartnerPostingsAsync(new FinanceDocumentExportRequest
            {
                EntityId = partnerId,
                Format = FinanceExportFormat.Xlsx
            });
            statement = await _statementService.GeneratePartnerStatementAsync(new FinanceStatementRequest
            {
                EntityId = partnerId
            });
        });

        balance.Balance.ShouldBe(25.00m);
        csv.PostingSum.ShouldBe(25.00m);
        xlsx.PostingSum.ShouldBe(25.00m);
        statement.PostingSum.ShouldBe(25.00m);
        FinanceExportTotalParser.ReadCsvTotal(csv.Content).ShouldBe(25.00m);
        FinanceExportTotalParser.ReadExcelTotal(xlsx.Content).ShouldBe(25.00m);
    }

    [Fact]
    public async Task Generated_Pdf_Contains_Zahy_Branding_And_Verified_Kyc()
    {
        var partnerId = Guid.NewGuid();
        const string submittedCr = "1010123456";
        const string verifiedCr = "2020987654";

        await SeedPartnerWithVerifiedKycAsync(
            partnerId,
            submittedCr: submittedCr,
            verifiedCr: verifiedCr,
            verifiedNameEn: "Verified Docs Partner Ltd",
            verifiedNameAr: "شركة معتمدة للمستندات");

        await WithUnitOfWorkAsync(async () =>
        {
            await _accountService.OpenPartnerAccountAsync(partnerId);
            await _ledgerService.AccrueAsync(CreateAccrualRequest(partnerId, Guid.NewGuid(), 100m, 15m));
        });

        _currentPartner.Id = partnerId;

        FinanceDocumentBytesResult statement = null!;
        await WithUnitOfWorkAsync(async () =>
        {
            statement = await _statementService.GeneratePartnerStatementAsync(new FinanceStatementRequest
            {
                EntityId = partnerId
            });
        });

        var pdfText = FinancePdfTestTextExtractor.ExtractText(statement.Content);
        pdfText.ShouldContain("Zahy");
        pdfText.ShouldContain("Account Statement");
        pdfText.ShouldContain("Verified KYC");
        pdfText.ShouldContain(verifiedCr);
        pdfText.ShouldContain("Verified Docs Partner Ltd");
        pdfText.ShouldNotContain(submittedCr);
        pdfText.ShouldContain("BETA"); // beta posture: generated documents are marked a test document
    }

    [Fact]
    public async Task Generated_Invoice_Pdf_Is_Marked_Beta_Not_Valid_Tax_Invoice()
    {
        var partnerId = Guid.NewGuid();
        await SeedPartnerWithPostingsAsync(partnerId, platformAmount: 10m, partnerEarnsAmount: 0m, billingAmount: 20m);
        _currentPartner.Id = partnerId;

        FinanceDocumentBytesResult invoice = null!;
        await WithUnitOfWorkAsync(async () =>
        {
            invoice = await _invoiceService.GeneratePartnerInvoiceAsync(new FinanceInvoiceRequest
            {
                PartnerId = partnerId
            });
        });

        var text = FinancePdfTestTextExtractor.ExtractText(invoice.Content);
        text.ShouldContain("BETA");
        text.ShouldContain("NOT A VALID TAX INVOICE");
    }

    [Fact]
    public async Task Statement_Renders_For_Partner_And_Merchant()
    {
        var partnerId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();

        await SeedPartnerWithPostingsAsync(partnerId, 10m, 0m, 5m);
        await SeedMerchantWithPostingsAsync(tenantId, billingAmount: 30m);

        _currentPartner.Id = partnerId;
        _currentTenant.Id = tenantId;

        FinanceDocumentBytesResult partnerStatement = null!;
        FinanceDocumentBytesResult merchantStatement = null!;

        await WithUnitOfWorkAsync(async () =>
        {
            partnerStatement = await _statementService.GeneratePartnerStatementAsync(new FinanceStatementRequest
            {
                EntityId = partnerId
            });
            merchantStatement = await _statementService.GenerateMerchantStatementAsync(new FinanceStatementRequest
            {
                EntityId = tenantId
            });
        });

        FinancePdfTestTextExtractor.ExtractText(partnerStatement.Content)
            .ShouldContain("Partner Account");
        FinancePdfTestTextExtractor.ExtractText(merchantStatement.Content)
            .ShouldContain("Merchant Account");
        partnerStatement.PostingSum.ShouldBe(15.00m);
        merchantStatement.PostingSum.ShouldBe(30.00m);
    }

    [Fact]
    public async Task Partner_Cannot_Export_Other_Partner_Statement()
    {
        var partnerA = Guid.NewGuid();
        var partnerB = Guid.NewGuid();
        await SeedPartnerWithPostingsAsync(partnerA, 10m, 0m, 0m);

        _currentPartner.Id = partnerB;

        await WithUnitOfWorkAsync(async () =>
        {
            await Should.ThrowAsync<AbpAuthorizationException>(async () =>
            {
                await _exportService.ExportPartnerPostingsAsync(new FinanceDocumentExportRequest
                {
                    EntityId = partnerA,
                    Format = FinanceExportFormat.Csv
                });
            });
        });
    }

    [Fact]
    public async Task Merchant_Cannot_Export_Other_Merchant_Statement()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        await SeedMerchantWithPostingsAsync(tenantA, 12m);

        _currentTenant.Id = tenantB;

        await WithUnitOfWorkAsync(async () =>
        {
            await Should.ThrowAsync<AbpAuthorizationException>(async () =>
            {
                await _statementService.GenerateMerchantStatementAsync(new FinanceStatementRequest
                {
                    EntityId = tenantA
                });
            });
        });
    }

    private async Task SeedPartnerWithPostingsAsync(
        Guid partnerId,
        decimal platformAmount,
        decimal partnerEarnsAmount,
        decimal billingAmount)
    {
        await WithUnitOfWorkAsync(async () =>
        {
            await SeedPartnerWithVerifiedKycAsync(partnerId);
            await _accountService.OpenPartnerAccountAsync(partnerId);

            if (platformAmount > 0)
            {
                await _ledgerService.AccrueAsync(CreateAccrualRequest(
                    partnerId,
                    Guid.NewGuid(),
                    100m,
                    platformAmount,
                    CommissionDirection.PlatformEarns,
                    "export-platform"));
            }

            if (partnerEarnsAmount > 0)
            {
                await _ledgerService.AccrueAsync(CreateAccrualRequest(
                    partnerId,
                    Guid.NewGuid(),
                    50m,
                    partnerEarnsAmount,
                    CommissionDirection.PartnerEarns,
                    "export-partner-earns"));
            }

            if (billingAmount > 0)
            {
                await _billingChargeService.ChargeAsync(new BillingChargeRequest
                {
                    PartnerId = partnerId,
                    ChargeTarget = BillingChargeTarget.Partner,
                    Kind = BillingChargeKind.ActivationFee,
                    Amount = billingAmount,
                    IdempotencyKey = BillingIdempotency.BuildActivationKey(partnerId)
                });
            }
        });
    }

    private async Task SeedMerchantWithPostingsAsync(Guid tenantId, decimal billingAmount)
    {
        await WithUnitOfWorkAsync(async () =>
        {
            await SeedMerchantWithVerifiedKycAsync(tenantId);
            await _accountService.OpenMerchantAccountAsync(tenantId);

            await _billingChargeService.ChargeAsync(new BillingChargeRequest
            {
                PartnerId = Guid.NewGuid(),
                ChargeTarget = BillingChargeTarget.Merchant,
                TenantId = tenantId,
                Kind = BillingChargeKind.Subscription,
                Amount = billingAmount,
                IdempotencyKey = $"billing:merchant-sub:{tenantId:N}"
            });
        });
    }

    private async Task SeedPartnerWithVerifiedKycAsync(
        Guid partnerId,
        string submittedCr = "1010111111",
        string verifiedCr = "2020222222",
        string verifiedNameEn = "Verified Partner Ltd",
        string verifiedNameAr = "شركة معتمدة")
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
                    CommercialRegistrationNumber = _fieldProtector.Protect(submittedCr),
                    VatNumber = _fieldProtector.Protect("300011111111111"),
                    Iban = _fieldProtector.Protect("SA9922334455667788990011"),
                    LegalAddress = _fieldProtector.Protect("Submitted Address")
                }),
            autoSave: true);

        var verification = KycVerification.CreateForSubmission(
            verificationId,
            KycEntityKind.Partner,
            partnerId,
            submissionId);
        verification.MarkUnderReview(Guid.NewGuid(), DateTime.UtcNow);
        verification.MarkVerified(new KycVerifiedCanonicalFields
        {
            LegalNameAr = verifiedNameAr,
            LegalNameEn = verifiedNameEn,
            CommercialRegistrationNumber = verifiedCr,
            VatNumber = "300099988877766",
            Iban = "SA4420000000000012345678",
            LegalAddress = "Verified Address"
        }, Guid.NewGuid(), DateTime.UtcNow);

        await verificationRepo.InsertAsync(verification, autoSave: true);
    }

    private async Task SeedMerchantWithVerifiedKycAsync(Guid tenantId)
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

        var verification = KycVerification.CreateForSubmission(
            verificationId,
            KycEntityKind.Merchant,
            tenantId,
            submissionId);
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

    private async Task SeedPartnerWithVerifiedKycAsync(Guid partnerId) =>
        await SeedPartnerWithVerifiedKycAsync(partnerId, "1010111111", "2020222222");

    private static CommissionAccrualRequest CreateAccrualRequest(
        Guid partnerId,
        Guid ruleId,
        decimal basisAmount,
        decimal computedCommission,
        CommissionDirection direction = CommissionDirection.PlatformEarns,
        string sourceId = "doc-test") =>
        new()
        {
            PartnerId = partnerId,
            TenantId = Guid.NewGuid(),
            RuleId = ruleId,
            SourceType = "order.paid",
            SourceId = sourceId,
            OrderRecordId = Guid.NewGuid(),
            BasisAmount = basisAmount,
            ComputedCommission = computedCommission,
            Direction = direction,
            Currency = CommissionConsts.DefaultCurrency
        };
}
