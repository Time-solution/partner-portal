using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Volo.Abp.Application.Services;

namespace Zahy.Finance;

public class LocalZatcaInvoiceShaper : ApplicationService, IZatcaInvoiceShaper
{
    public ZatcaFatooraInvoiceDto Shape(FinanceInvoiceDraft draft) =>
        new()
        {
            Uuid = GuidGenerator.Create().ToString("D"),
            InvoiceNumber = draft.InvoiceNumber,
            IssueDate = draft.IssueDate,
            Currency = draft.Currency,
            TaxExclusiveAmount = draft.TaxExclusiveAmount,
            TaxAmount = draft.TaxAmount,
            TaxInclusiveAmount = draft.TaxInclusiveAmount,
            SellerLegalNameEn = draft.Seller.LegalNameEn,
            SellerVatNumber = draft.Seller.VatNumber,
            BuyerLegalNameEn = draft.Buyer.LegalNameEn,
            BuyerVatNumber = draft.Buyer.VatNumber,
            QrCodePlaceholder = "ZATCA-QR-OFFLINE-PLACEHOLDER"
        };
}

public class NullZatcaSubmitter : ApplicationService, IZatcaSubmitter
{
    public Task<ZatcaSubmissionResult> SubmitAsync(
        ZatcaFatooraInvoiceDto invoice,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new ZatcaSubmissionResult
        {
            Submitted = false,
            Message = "ZATCA submission is disabled in offline mode."
        });
}

public class FinanceDocumentExportService : ApplicationService, IFinanceDocumentExportService
{
    private readonly IFinancePostingReadService _postingReadService;

    public FinanceDocumentExportService(IFinancePostingReadService postingReadService)
    {
        _postingReadService = postingReadService;
    }

    public async Task<FinanceDocumentBytesResult> ExportPartnerPostingsAsync(
        FinanceDocumentExportRequest request,
        CancellationToken cancellationToken = default)
    {
        var ledger = await _postingReadService.GetPartnerLedgerAsync(
            request.EntityId,
            request.PeriodFrom,
            request.PeriodTo,
            cancellationToken);

        return BuildExportResult(request.Format, request.EntityId, ledger, FinanceDocumentKind.TransactionExport);
    }

    public async Task<FinanceDocumentBytesResult> ExportMerchantPostingsAsync(
        FinanceDocumentExportRequest request,
        CancellationToken cancellationToken = default)
    {
        var ledger = await _postingReadService.GetMerchantLedgerAsync(
            request.EntityId,
            request.PeriodFrom,
            request.PeriodTo,
            cancellationToken);

        return BuildExportResult(request.Format, request.EntityId, ledger, FinanceDocumentKind.TransactionExport);
    }

    private static FinanceDocumentBytesResult BuildExportResult(
        FinanceExportFormat format,
        Guid entityId,
        FinancePostingLedgerSnapshot ledger,
        FinanceDocumentKind documentKind)
    {
        var content = format switch
        {
            FinanceExportFormat.Xlsx => FinanceExcelExportGenerator.Generate(ledger),
            _ => FinanceCsvExportGenerator.Generate(ledger)
        };

        return new FinanceDocumentBytesResult
        {
            Content = content,
            ContentType = format == FinanceExportFormat.Xlsx
                ? "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
                : "text/csv",
            FileName = FinanceDocumentFileNames.BuildExportFileName(entityId, format),
            PostingSum = ledger.PostingSum,
            DocumentKind = documentKind
        };
    }
}

public class FinanceStatementDocumentService : ApplicationService, IFinanceStatementDocumentService
{
    private readonly IFinancePostingReadService _postingReadService;
    private readonly IFinanceDocumentKycBlockBuilder _kycBlockBuilder;
    private readonly FinanceBrandingOptions _branding;

    public FinanceStatementDocumentService(
        IFinancePostingReadService postingReadService,
        IFinanceDocumentKycBlockBuilder kycBlockBuilder,
        IOptions<FinanceBrandingOptions> branding)
    {
        _postingReadService = postingReadService;
        _kycBlockBuilder = kycBlockBuilder;
        _branding = branding.Value;
    }

    public async Task<FinanceDocumentBytesResult> GeneratePartnerStatementAsync(
        FinanceStatementRequest request,
        CancellationToken cancellationToken = default)
    {
        var ledger = await _postingReadService.GetPartnerLedgerAsync(
            request.EntityId,
            request.PeriodFrom,
            request.PeriodTo,
            cancellationToken);

        var verifiedKyc = await _kycBlockBuilder.BuildAsync(
            KycEntityKind.Partner,
            request.EntityId,
            cancellationToken);

        var pdf = FinancePdfDocumentGenerator.GenerateStatement(
            _branding,
            ledger,
            verifiedKyc,
            "Partner Account",
            "حساب الشريك");

        return new FinanceDocumentBytesResult
        {
            Content = pdf,
            ContentType = "application/pdf",
            FileName = FinanceDocumentFileNames.BuildStatementFileName(request.EntityId),
            PostingSum = ledger.PostingSum,
            DocumentKind = FinanceDocumentKind.Statement
        };
    }

    public async Task<FinanceDocumentBytesResult> GenerateMerchantStatementAsync(
        FinanceStatementRequest request,
        CancellationToken cancellationToken = default)
    {
        var ledger = await _postingReadService.GetMerchantLedgerAsync(
            request.EntityId,
            request.PeriodFrom,
            request.PeriodTo,
            cancellationToken);

        var verifiedKyc = await _kycBlockBuilder.BuildAsync(
            KycEntityKind.Merchant,
            request.EntityId,
            cancellationToken);

        var pdf = FinancePdfDocumentGenerator.GenerateStatement(
            _branding,
            ledger,
            verifiedKyc,
            "Merchant Account",
            "حساب التاجر");

        return new FinanceDocumentBytesResult
        {
            Content = pdf,
            ContentType = "application/pdf",
            FileName = FinanceDocumentFileNames.BuildStatementFileName(request.EntityId),
            PostingSum = ledger.PostingSum,
            DocumentKind = FinanceDocumentKind.Statement
        };
    }
}

public class FinanceInvoiceDocumentService : ApplicationService, IFinanceInvoiceDocumentService
{
    private readonly IFinancePostingReadService _postingReadService;
    private readonly IFinanceDocumentKycBlockBuilder _kycBlockBuilder;
    private readonly IZatcaInvoiceShaper _zatcaInvoiceShaper;
    private readonly FinanceBrandingOptions _branding;

    public FinanceInvoiceDocumentService(
        IFinancePostingReadService postingReadService,
        IFinanceDocumentKycBlockBuilder kycBlockBuilder,
        IZatcaInvoiceShaper zatcaInvoiceShaper,
        IOptions<FinanceBrandingOptions> branding)
    {
        _postingReadService = postingReadService;
        _kycBlockBuilder = kycBlockBuilder;
        _zatcaInvoiceShaper = zatcaInvoiceShaper;
        _branding = branding.Value;
    }

    public async Task<FinanceDocumentBytesResult> GeneratePartnerInvoiceAsync(
        FinanceInvoiceRequest request,
        CancellationToken cancellationToken = default)
    {
        var ledger = await _postingReadService.GetPartnerLedgerAsync(
            request.PartnerId,
            request.PeriodFrom,
            request.PeriodTo,
            cancellationToken);

        var verifiedKyc = await _kycBlockBuilder.BuildAsync(
            KycEntityKind.Partner,
            request.PartnerId,
            cancellationToken);

        var taxExclusive = ledger.PostingSum;
        var taxAmount = FinanceMoney.RoundPosting(taxExclusive * 0.15m);
        var draft = new FinanceInvoiceDraft
        {
            InvoiceNumber = $"ZAHY-INV-{Clock.Now:yyyy-MM-dd-HHmmss}",
            IssueDate = Clock.Now,
            TaxExclusiveAmount = taxExclusive,
            TaxAmount = taxAmount,
            TaxInclusiveAmount = FinanceMoney.RoundPosting(taxExclusive + taxAmount),
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
        var pdf = FinancePdfDocumentGenerator.GenerateInvoice(_branding, ledger, verifiedKyc, zatca);

        return new FinanceDocumentBytesResult
        {
            Content = pdf,
            ContentType = "application/pdf",
            FileName = FinanceDocumentFileNames.BuildInvoiceFileName(request.PartnerId),
            PostingSum = ledger.PostingSum,
            DocumentKind = FinanceDocumentKind.Invoice
        };
    }
}
