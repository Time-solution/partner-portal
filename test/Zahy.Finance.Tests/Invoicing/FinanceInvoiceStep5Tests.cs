using System;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.Uow;
using Xunit;
using Zahy.Commission;

namespace Zahy.Finance;

public class FinanceInvoiceStep5Tests : ZahyFinanceTestBase
{
    private readonly IFinanceInvoiceGenerationService _invoiceGenerationService;
    private readonly IFinanceInvoiceDocumentService _invoiceDocumentService;
    private readonly IFinanceAccountStatusService _accountStatusService;
    private readonly IMerchantOperationalStatusService _operationalStatusService;
    private readonly IFinanceAccountService _accountService;
    private readonly IBillingChargeService _billingChargeService;
    private readonly IRepository<FinanceDocument, Guid> _documentRepository;
    private readonly IRepository<InvoiceNumberSequence, Guid> _sequenceRepository;
    private readonly IRepository<FinanceAccountStatusAudit, Guid> _statusAuditRepository;
    private readonly IRepository<PartnerFinancialAccount, Guid> _partnerAccountRepository;
    private readonly IRepository<MerchantAccount, Guid> _merchantAccountRepository;
    private readonly IGuidGenerator _guidGenerator;
    private readonly IKycFieldProtector _fieldProtector;
    private readonly ConfigurableFinanceInvoicePdfGenerator _pdfGenerator;
    private readonly RecordingLoggerProvider _recordingLogger;
    private readonly TestCurrentPartner _currentPartner;

    public FinanceInvoiceStep5Tests()
    {
        _invoiceGenerationService = GetRequiredService<IFinanceInvoiceGenerationService>();
        _invoiceDocumentService = GetRequiredService<IFinanceInvoiceDocumentService>();
        _accountStatusService = GetRequiredService<IFinanceAccountStatusService>();
        _operationalStatusService = GetRequiredService<IMerchantOperationalStatusService>();
        _accountService = GetRequiredService<IFinanceAccountService>();
        _billingChargeService = GetRequiredService<IBillingChargeService>();
        _documentRepository = GetRequiredService<IRepository<FinanceDocument, Guid>>();
        _sequenceRepository = GetRequiredService<IRepository<InvoiceNumberSequence, Guid>>();
        _statusAuditRepository = GetRequiredService<IRepository<FinanceAccountStatusAudit, Guid>>();
        _partnerAccountRepository = GetRequiredService<IRepository<PartnerFinancialAccount, Guid>>();
        _merchantAccountRepository = GetRequiredService<IRepository<MerchantAccount, Guid>>();
        _guidGenerator = GetRequiredService<IGuidGenerator>();
        _fieldProtector = GetRequiredService<IKycFieldProtector>();
        _pdfGenerator = GetRequiredService<ConfigurableFinanceInvoicePdfGenerator>();
        _recordingLogger = GetRequiredService<RecordingLoggerProvider>();
        _currentPartner = GetRequiredService<TestCurrentPartner>();
        _currentPartner.Id = null;
        _pdfGenerator.FailNext = false;
    }

    [Fact]
    public async Task Manual_Generates_On_Demand()
    {
        var partnerId = Guid.NewGuid();
        await SeedPartnerWithBillingAsync(partnerId, 25m);
        _currentPartner.Id = partnerId;

        FinanceInvoiceGenerationResult result = null!;
        await WithUnitOfWorkAsync(async () =>
        {
            result = await _invoiceGenerationService.GeneratePartnerInvoiceOnDemandAsync(
                new FinanceInvoiceRequest { PartnerId = partnerId });
        });

        result.IsNew.ShouldBeTrue();
        result.InvoiceNumber.ShouldStartWith("ZAHY-INV-");
        result.PostingSum.ShouldBe(25.00m);
        result.Content.ShouldNotBeEmpty();

        await WithUnitOfWorkAsync(async () =>
        {
            (await _documentRepository.GetCountAsync()).ShouldBe(1);
        });
    }

    [Fact]
    public async Task Automatic_Fires_Once_Per_Event()
    {
        var partnerId = Guid.NewGuid();
        await SeedPartnerWithBillingAsync(partnerId, 12m);
        await SetPartnerInvoiceModeAsync(partnerId, InvoiceGenerationMode.Automatic);

        await WithUnitOfWorkAsync(async () =>
        {
            await _billingChargeService.ChargeAsync(new BillingChargeRequest
            {
                PartnerId = partnerId,
                ChargeTarget = BillingChargeTarget.Partner,
                Kind = BillingChargeKind.ActivationFee,
                Amount = 12m,
                IdempotencyKey = BillingIdempotency.BuildActivationKey(partnerId)
            });
        });

        await WithUnitOfWorkAsync(async () =>
        {
            (await _documentRepository.GetCountAsync()).ShouldBe(1);
        });
    }

    [Fact]
    public async Task Automatic_Does_Not_Duplicate_Invoice()
    {
        var partnerId = Guid.NewGuid();
        await SeedPartnerWithBillingAsync(partnerId, 0m);
        await SetPartnerInvoiceModeAsync(partnerId, InvoiceGenerationMode.Automatic);

        const string idempotencyKey = "billing:dup-invoice-test";

        await WithUnitOfWorkAsync(async () =>
        {
            await _billingChargeService.ChargeAsync(new BillingChargeRequest
            {
                PartnerId = partnerId,
                ChargeTarget = BillingChargeTarget.Partner,
                Kind = BillingChargeKind.ActivationFee,
                Amount = 10m,
                IdempotencyKey = idempotencyKey
            });
            await _billingChargeService.ChargeAsync(new BillingChargeRequest
            {
                PartnerId = partnerId,
                ChargeTarget = BillingChargeTarget.Partner,
                Kind = BillingChargeKind.ActivationFee,
                Amount = 10m,
                IdempotencyKey = idempotencyKey
            });
        });

        await WithUnitOfWorkAsync(async () =>
        {
            (await _documentRepository.GetCountAsync()).ShouldBe(1);
        });
    }

    [Fact]
    public async Task Suspended_Merchant_Is_Not_Operational()
    {
        var tenantId = Guid.NewGuid();
        await SeedMerchantAccountAsync(tenantId);

        (await _operationalStatusService.IsOperationalAsync(tenantId)).ShouldBeTrue();

        await WithUnitOfWorkAsync(async () =>
        {
            await _accountStatusService.ChangeMerchantStatusAsync(new FinanceAccountStatusChangeRequest
            {
                EntityId = tenantId,
                TargetStatus = FinanceAccountStatus.Suspended,
                Reason = "Review"
            });
        });

        (await _operationalStatusService.IsOperationalAsync(tenantId)).ShouldBeFalse();
    }

    [Fact]
    public async Task Illegal_Status_Transition_Rejected_And_Audited()
    {
        var partnerId = Guid.NewGuid();
        await SeedPartnerAccountAsync(partnerId);
        _recordingLogger.Messages.Clear();

        await WithUnitOfWorkAsync(async () =>
        {
            await _accountStatusService.ChangePartnerStatusAsync(new FinanceAccountStatusChangeRequest
            {
                EntityId = partnerId,
                TargetStatus = FinanceAccountStatus.Closed,
                Reason = "Close account"
            });
        });

        var auditCountBefore = await _statusAuditRepository.GetCountAsync();

        await WithUnitOfWorkAsync(async () =>
        {
            var ex = await Should.ThrowAsync<BusinessException>(() =>
                _accountStatusService.ChangePartnerStatusAsync(new FinanceAccountStatusChangeRequest
                {
                    EntityId = partnerId,
                    TargetStatus = FinanceAccountStatus.Active,
                    Reason = "Illegal reopen"
                }));

            ex.Code.ShouldBe(FinanceErrorCodes.IllegalStatusTransition);
        });

        (await _statusAuditRepository.GetCountAsync()).ShouldBe(auditCountBefore);
        _recordingLogger.Messages.ShouldNotContain(x => x.Contains("Closed -> Active"));
    }

    [Fact]
    public async Task Invoice_Number_Rolls_Back_When_Generation_Fails()
    {
        var partnerId = Guid.NewGuid();
        await SeedPartnerWithBillingAsync(partnerId, 15m);
        _currentPartner.Id = partnerId;
        const string idempotencyKey = "invoice:rollback-test";

        _pdfGenerator.FailNext = true;

        await Should.ThrowAsync<InvalidOperationException>(async () =>
        {
            await WithUnitOfWorkAsync(async () =>
            {
                await _invoiceGenerationService.GeneratePartnerInvoiceOnDemandAsync(
                    new FinanceInvoiceRequest
                    {
                        PartnerId = partnerId,
                        IdempotencyKey = idempotencyKey
                    });
            });
        });

        await WithUnitOfWorkAsync(async () =>
        {
            (await _documentRepository.GetCountAsync()).ShouldBe(0);
            var sequence = (await _sequenceRepository.GetListAsync()).SingleOrDefault();
            (sequence == null || sequence.LastNumber == 0).ShouldBeTrue();
        });

        _pdfGenerator.FailNext = false;

        FinanceInvoiceGenerationResult result = null!;
        await WithUnitOfWorkAsync(async () =>
        {
            result = await _invoiceGenerationService.GeneratePartnerInvoiceOnDemandAsync(
                new FinanceInvoiceRequest
                {
                    PartnerId = partnerId,
                    IdempotencyKey = idempotencyKey
                });
        });

        result.SequenceShouldBeFirstInvoice();
    }

    [Fact]
    public async Task Invoice_Idempotent_Retry_Returns_Same_Number()
    {
        var partnerId = Guid.NewGuid();
        await SeedPartnerWithBillingAsync(partnerId, 18m);
        _currentPartner.Id = partnerId;
        const string idempotencyKey = "invoice:idempotent-retry";

        FinanceInvoiceGenerationResult first = null!;
        FinanceInvoiceGenerationResult second = null!;

        await WithUnitOfWorkAsync(async () =>
        {
            first = await _invoiceGenerationService.GeneratePartnerInvoiceOnDemandAsync(
                new FinanceInvoiceRequest
                {
                    PartnerId = partnerId,
                    IdempotencyKey = idempotencyKey
                });
        });

        await WithUnitOfWorkAsync(async () =>
        {
            second = await _invoiceGenerationService.GeneratePartnerInvoiceOnDemandAsync(
                new FinanceInvoiceRequest
                {
                    PartnerId = partnerId,
                    IdempotencyKey = idempotencyKey
                });
        });

        first.IsNew.ShouldBeTrue();
        second.IsNew.ShouldBeFalse();
        second.InvoiceNumber.ShouldBe(first.InvoiceNumber);

        await WithUnitOfWorkAsync(async () =>
        {
            (await _documentRepository.GetCountAsync()).ShouldBe(1);
            var sequence = (await _sequenceRepository.GetListAsync()).Single();
            sequence.LastNumber.ShouldBe(1);
        });
    }

    private async Task SeedPartnerAccountAsync(Guid partnerId)
    {
        await WithUnitOfWorkAsync(async () =>
        {
            await SeedPartnerVerifiedKycAsync(partnerId);
            await _accountService.OpenPartnerAccountAsync(partnerId);
        });
    }

    private async Task SeedPartnerWithBillingAsync(Guid partnerId, decimal billingAmount)
    {
        await WithUnitOfWorkAsync(async () =>
        {
            await SeedPartnerVerifiedKycAsync(partnerId);
            await _accountService.OpenPartnerAccountAsync(partnerId);

            if (billingAmount > 0)
            {
                await _billingChargeService.ChargeAsync(new BillingChargeRequest
                {
                    PartnerId = partnerId,
                    ChargeTarget = BillingChargeTarget.Partner,
                    Kind = BillingChargeKind.Subscription,
                    Amount = billingAmount,
                    IdempotencyKey = $"billing:seed:{partnerId:N}"
                });
            }
        });
    }

    private async Task SeedMerchantAccountAsync(Guid tenantId)
    {
        await WithUnitOfWorkAsync(async () =>
        {
            await SeedMerchantVerifiedKycAsync(tenantId);
            await _accountService.OpenMerchantAccountAsync(tenantId);
        });
    }

    private async Task SetPartnerInvoiceModeAsync(Guid partnerId, InvoiceGenerationMode mode)
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var queryable = await _partnerAccountRepository.GetQueryableAsync();
            var account = queryable.Single(x => x.PartnerId == partnerId);
            account.SetInvoiceGenerationModeOverride(mode);
            await _partnerAccountRepository.UpdateAsync(account, autoSave: true);
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

        var verification = KycVerification.CreateForSubmission(
            verificationId,
            KycEntityKind.Partner,
            partnerId,
            submissionId);
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
}

internal static class FinanceInvoiceStep5TestExtensions
{
    public static void SequenceShouldBeFirstInvoice(this FinanceInvoiceGenerationResult result)
    {
        var year = DateTime.UtcNow.Year;
        result.InvoiceNumber.ShouldBe(FinanceInvoiceNumberFormat.Format(year, 1));
    }
}
