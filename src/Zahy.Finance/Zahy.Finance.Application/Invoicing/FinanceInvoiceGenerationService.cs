using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.Uow;
using Zahy.Commission;

namespace Zahy.Finance;

public class FinanceInvoiceGenerationService : ApplicationService, IFinanceInvoiceGenerationService
{
    private readonly IFinancePostingReadService _postingReadService;
    private readonly IFinanceDocumentKycBlockBuilder _kycBlockBuilder;
    private readonly IZatcaInvoiceShaper _zatcaInvoiceShaper;
    private readonly IFinanceInvoiceNumberAllocator _invoiceNumberAllocator;
    private readonly IFinanceInvoicePdfGenerator _invoicePdfGenerator;
    private readonly FinanceBrandingOptions _branding;
    private readonly IRepository<FinanceDocument, Guid> _documentRepository;
    private readonly IRepository<PartnerFinancialAccount, Guid> _partnerAccountRepository;
    private readonly IRepository<MerchantAccount, Guid> _merchantAccountRepository;
    private readonly IGuidGenerator _guidGenerator;
    private readonly IFinancePeriodStatusProvider _periodStatusProvider;
    private readonly decimal _vatRate;

    public FinanceInvoiceGenerationService(
        IFinancePostingReadService postingReadService,
        IFinanceDocumentKycBlockBuilder kycBlockBuilder,
        IZatcaInvoiceShaper zatcaInvoiceShaper,
        IFinanceInvoiceNumberAllocator invoiceNumberAllocator,
        IFinanceInvoicePdfGenerator invoicePdfGenerator,
        IOptions<FinanceBrandingOptions> branding,
        IOptions<FinanceVatOptions> vat,
        IRepository<FinanceDocument, Guid> documentRepository,
        IRepository<PartnerFinancialAccount, Guid> partnerAccountRepository,
        IRepository<MerchantAccount, Guid> merchantAccountRepository,
        IGuidGenerator guidGenerator,
        IFinancePeriodStatusProvider periodStatusProvider)
    {
        _postingReadService = postingReadService;
        _kycBlockBuilder = kycBlockBuilder;
        _zatcaInvoiceShaper = zatcaInvoiceShaper;
        _invoiceNumberAllocator = invoiceNumberAllocator;
        _invoicePdfGenerator = invoicePdfGenerator;
        _branding = branding.Value;
        _vatRate = vat.Value.StandardRate;
        _documentRepository = documentRepository;
        _partnerAccountRepository = partnerAccountRepository;
        _merchantAccountRepository = merchantAccountRepository;
        _guidGenerator = guidGenerator;
        _periodStatusProvider = periodStatusProvider;
    }

    [UnitOfWork]
    public virtual Task<FinanceInvoiceGenerationResult> GeneratePartnerInvoiceOnDemandAsync(
        FinanceInvoiceRequest request,
        CancellationToken cancellationToken = default)
    {
        var idempotencyKey = request.IdempotencyKey ??
                             $"invoice:manual:{request.PartnerId:N}:{Clock.Now:yyyyMMddHHmmssfff}";

        return GenerateInternalAsync(
            FinanceAccountKind.Partner,
            request.PartnerId,
            request.PartnerId,
            idempotencyKey,
            request.PeriodFrom,
            request.PeriodTo,
            sourceRowId: null,
            cancellationToken);
    }

    [UnitOfWork]
    public virtual Task<FinanceInvoiceGenerationResult> RegenerateInvoiceByIdempotencyAsync(
        FinanceAccountKind accountKind,
        Guid entityId,
        string idempotencyKey,
        CancellationToken cancellationToken = default) =>
        GenerateInternalAsync(
            accountKind,
            entityId,
            entityId,
            idempotencyKey,
            periodFrom: null,
            periodTo: null,
            sourceRowId: null,
            cancellationToken);

    [UnitOfWork]
    public virtual Task<FinanceInvoiceGenerationResult> TryGenerateForBillingChargeAsync(
        FinanceBillingInvoiceTriggerContext context,
        CancellationToken cancellationToken = default)
    {
        var idempotencyKey = FinanceDocumentIdempotency.BuildBillingInvoiceKey(context.BillingChargeId);

        if (context.AccountKind == FinanceAccountKind.Merchant)
        {
            if (context.TenantId == null)
            {
                throw new BusinessException(FinanceErrorCodes.InvalidPosting)
                    .WithData("BillingChargeId", context.BillingChargeId);
            }

            return GenerateInternalAsync(
                FinanceAccountKind.Merchant,
                context.TenantId.Value,
                context.TenantId.Value,
                idempotencyKey,
                periodFrom: null,
                periodTo: null,
                context.BillingChargeId,
                cancellationToken);
        }

        return GenerateInternalAsync(
            FinanceAccountKind.Partner,
            context.PartnerId,
            context.PartnerId,
            idempotencyKey,
            periodFrom: null,
            periodTo: null,
            context.BillingChargeId,
            cancellationToken);
    }

    private async Task<FinanceInvoiceGenerationResult> GenerateInternalAsync(
        FinanceAccountKind accountKind,
        Guid entityId,
        Guid accountEntityId,
        string idempotencyKey,
        DateTime? periodFrom,
        DateTime? periodTo,
        Guid? sourceRowId,
        CancellationToken cancellationToken)
    {
        var existing = await FindDocumentByIdempotencyAsync(idempotencyKey, cancellationToken);
        if (existing != null)
        {
            return await BuildResultFromExistingAsync(existing, cancellationToken);
        }

        if (accountKind == FinanceAccountKind.Partner)
        {
            await FindPartnerAccountAsync(accountEntityId, cancellationToken);
        }
        else
        {
            await FindMerchantAccountAsync(accountEntityId, cancellationToken);
        }

        FinancePostingLedgerSnapshot ledger = accountKind == FinanceAccountKind.Partner
            ? await _postingReadService.GetPartnerLedgerAsync(
                accountEntityId,
                periodFrom,
                periodTo,
                cancellationToken)
            : await _postingReadService.GetMerchantLedgerAsync(
                accountEntityId,
                periodFrom,
                periodTo,
                cancellationToken);

        var verifiedKyc = await _kycBlockBuilder.BuildAsync(
            accountKind == FinanceAccountKind.Partner ? KycEntityKind.Partner : KycEntityKind.Merchant,
            accountEntityId,
            cancellationToken);

        var accountId = accountKind == FinanceAccountKind.Partner
            ? (await FindPartnerAccountAsync(accountEntityId, cancellationToken)).Id
            : (await FindMerchantAccountAsync(accountEntityId, cancellationToken)).Id;

        var issueDate = Clock.Now;
        var allocation = await _invoiceNumberAllocator.AllocateInvoiceNumberAsync(issueDate, cancellationToken);
        var pdf = BuildInvoicePdf(ledger, verifiedKyc, allocation.InvoiceNumber, issueDate);

        var document = FinanceDocument.CreateInvoice(
            _guidGenerator.Create(),
            accountKind,
            accountId,
            entityId,
            idempotencyKey,
            allocation.InvoiceNumber,
            allocation.FiscalYear,
            allocation.SequenceNumber,
            ledger.PostingSum,
            issueDate,
            sourceRowId);

        // P5 — call site B: LedgerDerived documents are born Issued and never pass Issue(), so the
        // SAME gate runs here BEFORE persistence. The ledger figure is the read-only tie for :087;
        // nothing is persisted when the gate throws.
        var duplicateQueryable = await _documentRepository.GetQueryableAsync();
        FinanceInvoicePreIssueGate.EnsureValid(document, new FinanceInvoiceGateContext
        {
            NowUtc = Clock.Now, // SAME IClock basis as the stored GeneratedAt (kind-agnostic compare)
            VatRate = _vatRate,
            DuplicateReferenceExists = duplicateQueryable.Any(
                x => x.InvoiceNumber == document.InvoiceNumber && x.Id != document.Id),
            PeriodOpen = await _periodStatusProvider.IsPeriodOpenAsync(document.GeneratedAt, cancellationToken),
            LedgerSourceFigure = ledger.PostingSum
        });

        await _documentRepository.InsertAsync(document, autoSave: false, cancellationToken: cancellationToken);

        return new FinanceInvoiceGenerationResult
        {
            DocumentId = document.Id,
            InvoiceNumber = document.InvoiceNumber,
            Content = pdf,
            PostingSum = document.PostingSum,
            IsNew = true,
            ContentType = "application/pdf",
            FileName = FinanceDocumentFileNames.BuildInvoiceFileName(entityId)
        };
    }

    private byte[] BuildInvoicePdf(
        FinancePostingLedgerSnapshot ledger,
        FinanceDocumentKycBlockDto verifiedKyc,
        string invoiceNumber,
        DateTime issueDate)
    {
        // PostingSum is VAT-INCLUSIVE (billing charges / commission accruals cross the module boundary as
        // inclusive amounts — the SAME convention the Settlement engine uses). Split it via the inclusive
        // back-out so net + VAT == PostingSum exactly, instead of (incorrectly) adding 15% on top.
        var taxInclusive = ledger.PostingSum;
        var taxExclusive = FinanceVat.NetOfInclusive(taxInclusive, _vatRate);
        var taxAmount = FinanceVat.VatOfInclusive(taxInclusive, _vatRate);
        var draft = new FinanceInvoiceDraft
        {
            InvoiceNumber = invoiceNumber,
            IssueDate = issueDate,
            TaxExclusiveAmount = taxExclusive,
            TaxAmount = taxAmount,
            TaxInclusiveAmount = taxInclusive,
            Currency = ledger.Currency,
            Seller = new FinanceDocumentKycBlockDto
            {
                LegalNameEn = _branding.PlatformLegalNameEn,
                LegalNameAr = _branding.PlatformLegalNameAr,
                CommercialRegistrationNumber = "0000000000",
                VatNumber = "300000000000003"
            },
            Buyer = verifiedKyc
        };

        var zatca = _zatcaInvoiceShaper.Shape(draft);
        return _invoicePdfGenerator.GenerateInvoice(_branding, ledger, verifiedKyc, zatca);
    }

    private async Task<FinanceInvoiceGenerationResult> BuildResultFromExistingAsync(
        FinanceDocument document,
        CancellationToken cancellationToken)
    {
        FinancePostingLedgerSnapshot ledger;
        FinanceDocumentKycBlockDto verifiedKyc;

        if (document.AccountKind == FinanceAccountKind.Partner)
        {
            ledger = await _postingReadService.GetPartnerLedgerAsync(document.EntityId, cancellationToken: cancellationToken);
            verifiedKyc = await _kycBlockBuilder.BuildAsync(
                KycEntityKind.Partner,
                document.EntityId,
                cancellationToken);
        }
        else
        {
            ledger = await _postingReadService.GetMerchantLedgerAsync(document.EntityId, cancellationToken: cancellationToken);
            verifiedKyc = await _kycBlockBuilder.BuildAsync(
                KycEntityKind.Merchant,
                document.EntityId,
                cancellationToken);
        }

        var pdf = BuildInvoicePdf(ledger, verifiedKyc, document.InvoiceNumber, document.GeneratedAt);

        return new FinanceInvoiceGenerationResult
        {
            DocumentId = document.Id,
            InvoiceNumber = document.InvoiceNumber,
            Content = pdf,
            PostingSum = document.PostingSum,
            IsNew = false,
            ContentType = "application/pdf",
            FileName = FinanceDocumentFileNames.BuildInvoiceFileName(document.EntityId)
        };
    }

    private async Task<FinanceDocument?> FindDocumentByIdempotencyAsync(
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        var queryable = await _documentRepository.GetQueryableAsync();
        return queryable.FirstOrDefault(x => x.IdempotencyKey == idempotencyKey);
    }

    private async Task<PartnerFinancialAccount> FindPartnerAccountAsync(
        Guid partnerId,
        CancellationToken cancellationToken)
    {
        var queryable = await _partnerAccountRepository.GetQueryableAsync();
        var account = queryable.FirstOrDefault(x => x.PartnerId == partnerId);
        if (account == null)
        {
            throw new BusinessException(FinanceErrorCodes.AccountNotFound)
                .WithData("PartnerId", partnerId);
        }

        return account;
    }

    private async Task<MerchantAccount> FindMerchantAccountAsync(
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var queryable = await _merchantAccountRepository.GetQueryableAsync();
        var account = queryable.FirstOrDefault(x => x.TenantId == tenantId);
        if (account == null)
        {
            throw new BusinessException(FinanceErrorCodes.AccountNotFound)
                .WithData("TenantId", tenantId);
        }

        return account;
    }
}

public class BillingChargeInvoiceTrigger : ApplicationService, IInvoiceTrigger
{
    private readonly IFinanceInvoiceGenerationService _invoiceGenerationService;
    private readonly FinancePlatformSettingsProvider _platformSettingsProvider;
    private readonly IRepository<PartnerFinancialAccount, Guid> _partnerAccountRepository;
    private readonly IRepository<MerchantAccount, Guid> _merchantAccountRepository;
    private readonly IRepository<FinanceDocument, Guid> _documentRepository;

    public BillingChargeInvoiceTrigger(
        IFinanceInvoiceGenerationService invoiceGenerationService,
        FinancePlatformSettingsProvider platformSettingsProvider,
        IRepository<PartnerFinancialAccount, Guid> partnerAccountRepository,
        IRepository<MerchantAccount, Guid> merchantAccountRepository,
        IRepository<FinanceDocument, Guid> documentRepository)
    {
        _invoiceGenerationService = invoiceGenerationService;
        _platformSettingsProvider = platformSettingsProvider;
        _partnerAccountRepository = partnerAccountRepository;
        _merchantAccountRepository = merchantAccountRepository;
        _documentRepository = documentRepository;
    }

    public async Task TryGenerateForBillingChargeAsync(
        FinanceBillingInvoiceTriggerContext context,
        CancellationToken cancellationToken = default)
    {
        var idempotencyKey = FinanceDocumentIdempotency.BuildBillingInvoiceKey(context.BillingChargeId);
        var queryable = await _documentRepository.GetQueryableAsync();
        if (queryable.Any(x => x.IdempotencyKey == idempotencyKey))
        {
            await _invoiceGenerationService.TryGenerateForBillingChargeAsync(context, cancellationToken);
            return;
        }

        if (!context.IsNew)
        {
            return;
        }

        var mode = await ResolveEffectiveModeAsync(context, cancellationToken);
        if (mode != InvoiceGenerationMode.Automatic)
        {
            return;
        }

        await _invoiceGenerationService.TryGenerateForBillingChargeAsync(context, cancellationToken);
    }

    private async Task<InvoiceGenerationMode> ResolveEffectiveModeAsync(
        FinanceBillingInvoiceTriggerContext context,
        CancellationToken cancellationToken)
    {
        var platformDefault = await _platformSettingsProvider.GetDefaultInvoiceGenerationModeAsync(cancellationToken);

        if (context.AccountKind == FinanceAccountKind.Merchant)
        {
            if (context.TenantId == null)
            {
                return platformDefault;
            }

            var merchantQueryable = await _merchantAccountRepository.GetQueryableAsync();
            var merchantAccount = merchantQueryable.FirstOrDefault(x => x.TenantId == context.TenantId);
            return merchantAccount?.ResolveInvoiceGenerationMode(platformDefault) ?? platformDefault;
        }

        var partnerQueryable = await _partnerAccountRepository.GetQueryableAsync();
        var partnerAccount = partnerQueryable.FirstOrDefault(x => x.PartnerId == context.PartnerId);
        return partnerAccount?.ResolveInvoiceGenerationMode(platformDefault) ?? platformDefault;
    }
}
