using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;
using Zahy.Identity.Permissions;

namespace Zahy.Finance;

/// <summary>
/// Ad-hoc (manual) invoice authoring. ADDITIVE to <see cref="FinanceInvoiceGenerationService"/> — it does
/// NOT touch the ledger-sourced path. Validates input, splits VAT per line via <see cref="FinanceVat"/>
/// (F1's authority — unchanged), mints a number from the SAME pool/policy as the ledger path, embeds a
/// KYC block for parity, persists the document with <see cref="FinanceDocumentSource.Manual"/> + lines, and
/// is idempotent on <c>IdempotencyKey</c>. The PDF is built from the keyed lines, never from the ledger.
/// </summary>
public class FinanceManualInvoiceAppService : ApplicationService, IFinanceDocumentAppService
{
    private readonly IRepository<FinanceDocument, Guid> _documentRepository;
    private readonly IFinanceInvoiceNumberAllocator _invoiceNumberAllocator;
    private readonly IFinanceInvoiceGenerationService _invoiceGenerationService;
    private readonly IKycCanonicalProfileProvider _kycProfileProvider;
    private readonly IZatcaInvoiceShaper _zatcaInvoiceShaper;
    private readonly FinanceBrandingOptions _branding;
    private readonly decimal _vatRate;

    public FinanceManualInvoiceAppService(
        IRepository<FinanceDocument, Guid> documentRepository,
        IFinanceInvoiceNumberAllocator invoiceNumberAllocator,
        IFinanceInvoiceGenerationService invoiceGenerationService,
        IKycCanonicalProfileProvider kycProfileProvider,
        IZatcaInvoiceShaper zatcaInvoiceShaper,
        IOptions<FinanceBrandingOptions> branding,
        IOptions<FinanceVatOptions> vat)
    {
        _documentRepository = documentRepository;
        _invoiceNumberAllocator = invoiceNumberAllocator;
        _invoiceGenerationService = invoiceGenerationService;
        _kycProfileProvider = kycProfileProvider;
        _zatcaInvoiceShaper = zatcaInvoiceShaper;
        _branding = branding.Value;
        _vatRate = vat.Value.StandardRate;
    }

    [Authorize(ZahyPermissions.Finance.WriteManualInvoice)]
    [UnitOfWork]
    public virtual async Task<FinanceDocumentDto> CreateManualInvoiceAsync(
        CreateManualInvoiceRequest request,
        CancellationToken cancellationToken = default)
    {
        Check.NotNull(request, nameof(request));
        Check.NotNullOrWhiteSpace(request.IdempotencyKey, nameof(request.IdempotencyKey));

        if (request.Lines == null || request.Lines.Count == 0)
        {
            throw new BusinessException(FinanceErrorCodes.ManualInvoiceInvalidLines)
                .WithData("Reason", "At least one line is required.");
        }

        var idempotencyKey = request.IdempotencyKey.Trim();
        var existing = await FindByIdempotencyAsync(idempotencyKey, cancellationToken);
        if (existing != null)
        {
            return await MapToDtoAsync(existing, includePdf: true, cancellationToken);
        }

        // VAT split per line uses the SAME inclusive back-out as the rest of Finance — F1 authority, untouched.
        var lines = new List<FinanceInvoiceLine>(request.Lines.Count);
        var lineNo = 1;
        foreach (var lineDto in request.Lines)
        {
            lines.Add(FinanceInvoiceLine.Create(
                lineNo,
                lineDto.Description,
                lineDto.Quantity,
                lineDto.UnitPriceInclusive,
                _vatRate,
                lineDto.AccountCode));
            lineNo++;
        }

        var postingSum = FinanceMoney.RoundPosting(lines.Sum(l => l.LineTotalInclusive));

        var recipientName = await ResolveRecipientNameAsync(request, cancellationToken);
        var issueDate = request.IssueDate ?? Clock.Now;
        var currency = string.IsNullOrWhiteSpace(request.Currency) ? FinanceConsts.DefaultCurrency : request.Currency.Trim();

        var allocation = await _invoiceNumberAllocator.AllocateInvoiceNumberAsync(issueDate, cancellationToken);

        var document = FinanceDocument.CreateManualInvoice(
            GuidGenerator.Create(),
            request.RecipientType,
            request.RecipientType == FinanceDocumentRecipientType.External ? null : request.RecipientReference,
            recipientName,
            idempotencyKey,
            allocation.InvoiceNumber,
            allocation.FiscalYear,
            allocation.SequenceNumber,
            lines,
            postingSum,
            issueDate,
            request.Notes,
            currency);

        await _documentRepository.InsertAsync(document, autoSave: true, cancellationToken: cancellationToken);

        return await MapToDtoAsync(document, includePdf: true, cancellationToken);
    }

    public virtual async Task<FinanceDocumentDto> GetAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var document = await _documentRepository.GetAsync(id, cancellationToken: cancellationToken);
        return await MapToDtoAsync(document, includePdf: false, cancellationToken);
    }

    public virtual async Task<FinanceDocumentBytesResult> GetPdfAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var document = await _documentRepository.GetAsync(id, cancellationToken: cancellationToken);

        if (document.Source == FinanceDocumentSource.Manual)
        {
            var pdf = await BuildManualPdfAsync(document, cancellationToken);
            return new FinanceDocumentBytesResult
            {
                Content = pdf,
                ContentType = "application/pdf",
                FileName = FinanceDocumentFileNames.BuildInvoiceFileName(document.Id),
                PostingSum = document.PostingSum,
                DocumentKind = FinanceDocumentKind.Invoice
            };
        }

        // LedgerDerived: reuse the existing ledger-sourced generator, unchanged.
        var result = await _invoiceGenerationService.RegenerateInvoiceByIdempotencyAsync(
            document.AccountKind,
            document.EntityId,
            document.IdempotencyKey,
            cancellationToken);

        return new FinanceDocumentBytesResult
        {
            Content = result.Content,
            ContentType = result.ContentType,
            FileName = result.FileName,
            PostingSum = result.PostingSum,
            DocumentKind = FinanceDocumentKind.Invoice
        };
    }

    private async Task<string> ResolveRecipientNameAsync(
        CreateManualInvoiceRequest request,
        CancellationToken cancellationToken)
    {
        // Explicit override always wins (also the only source for External recipients).
        if (!string.IsNullOrWhiteSpace(request.RecipientNameOverride))
        {
            return request.RecipientNameOverride.Trim();
        }

        if (request.RecipientType == FinanceDocumentRecipientType.External)
        {
            throw new BusinessException(FinanceErrorCodes.ManualInvoiceInvalidLines)
                .WithData("Reason", "External recipients require RecipientNameOverride.");
        }

        if (request.RecipientReference == null)
        {
            throw new BusinessException(FinanceErrorCodes.ManualInvoiceInvalidLines)
                .WithData("Reason", "Partner/Merchant recipients require RecipientReference.");
        }

        var kind = request.RecipientType == FinanceDocumentRecipientType.Merchant
            ? KycEntityKind.Merchant
            : KycEntityKind.Partner;

        var profile = await _kycProfileProvider.GetLatestVerifiedProfileAsync(
            kind,
            request.RecipientReference.Value,
            cancellationToken);

        if (profile != null && !string.IsNullOrWhiteSpace(profile.LegalNameEn))
        {
            return profile.LegalNameEn;
        }

        return $"{request.RecipientType} {request.RecipientReference.Value:N}";
    }

    private async Task<FinanceDocumentKycBlockDto> BuildRecipientKycBlockAsync(
        FinanceDocument document,
        CancellationToken cancellationToken)
    {
        if (document.RecipientType is FinanceDocumentRecipientType.Partner or FinanceDocumentRecipientType.Merchant
            && document.RecipientReference != null)
        {
            var kind = document.RecipientType == FinanceDocumentRecipientType.Merchant
                ? KycEntityKind.Merchant
                : KycEntityKind.Partner;

            var profile = await _kycProfileProvider.GetLatestVerifiedProfileAsync(
                kind,
                document.RecipientReference.Value,
                cancellationToken);

            if (profile != null)
            {
                return new FinanceDocumentKycBlockDto
                {
                    LegalNameAr = profile.LegalNameAr,
                    LegalNameEn = profile.LegalNameEn,
                    CommercialRegistrationNumber = profile.CommercialRegistrationNumber,
                    VatNumber = profile.VatNumber,
                    Iban = profile.Iban,
                    LegalAddress = profile.LegalAddress
                };
            }
        }

        // External recipient (or no verified profile): minimal block with just the recipient name.
        return new FinanceDocumentKycBlockDto { LegalNameEn = document.Recipient ?? string.Empty };
    }

    private async Task<byte[]> BuildManualPdfAsync(
        FinanceDocument document,
        CancellationToken cancellationToken)
    {
        var recipientKyc = await BuildRecipientKycBlockAsync(document, cancellationToken);

        var taxExclusive = FinanceVat.NetOfInclusive(document.PostingSum, _vatRate);
        var taxAmount = FinanceVat.VatOfInclusive(document.PostingSum, _vatRate);
        var draft = new FinanceInvoiceDraft
        {
            InvoiceNumber = document.InvoiceNumber,
            IssueDate = document.GeneratedAt,
            TaxExclusiveAmount = taxExclusive,
            TaxAmount = taxAmount,
            TaxInclusiveAmount = document.PostingSum,
            Currency = document.Currency,
            Seller = new FinanceDocumentKycBlockDto
            {
                LegalNameEn = _branding.PlatformLegalNameEn,
                LegalNameAr = _branding.PlatformLegalNameAr,
                CommercialRegistrationNumber = "0000000000",
                VatNumber = "300000000000003"
            },
            Buyer = recipientKyc
        };

        var zatca = _zatcaInvoiceShaper.Shape(draft);
        return FinancePdfDocumentGenerator.GenerateManualInvoice(_branding, document, recipientKyc, zatca);
    }

    private async Task<FinanceDocument?> FindByIdempotencyAsync(
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        var queryable = await _documentRepository.GetQueryableAsync();
        return queryable.FirstOrDefault(x => x.IdempotencyKey == idempotencyKey);
    }

    private async Task<FinanceDocumentDto> MapToDtoAsync(
        FinanceDocument document,
        bool includePdf,
        CancellationToken cancellationToken)
    {
        var lines = document.Lines
            .OrderBy(l => l.LineNo)
            .Select(l => new FinanceInvoiceLineDto
            {
                LineNo = l.LineNo,
                Description = l.Description,
                Quantity = l.Quantity,
                UnitPriceInclusive = l.UnitPriceInclusive,
                LineTotalInclusive = l.LineTotalInclusive,
                VatNet = l.VatNet,
                VatAmount = l.VatAmount,
                AccountCode = l.AccountCode
            })
            .ToList();

        var dto = new FinanceDocumentDto
        {
            Id = document.Id,
            InvoiceNumber = document.InvoiceNumber,
            FiscalYear = document.FiscalYear,
            SequenceNumber = document.SequenceNumber,
            Source = document.Source,
            Recipient = document.Recipient,
            RecipientType = document.RecipientType,
            RecipientReference = document.RecipientReference,
            Notes = document.Notes,
            Currency = document.Currency,
            IssueDate = document.GeneratedAt,
            PostingSum = document.PostingSum,
            SubtotalNet = FinanceMoney.RoundPosting(lines.Sum(l => l.VatNet)),
            VatTotal = FinanceMoney.RoundPosting(lines.Sum(l => l.VatAmount)),
            GrandTotalInclusive = FinanceMoney.RoundPosting(lines.Sum(l => l.LineTotalInclusive)),
            Lines = lines,
            PdfDownloadToken = document.Id
        };

        if (includePdf && document.Source == FinanceDocumentSource.Manual)
        {
            var pdf = await BuildManualPdfAsync(document, cancellationToken);
            dto.PdfBase64 = Convert.ToBase64String(pdf);
        }

        return dto;
    }
}
