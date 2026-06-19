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
    private readonly IFinanceInvoiceGenerationService _invoiceGenerationService;

    public FinanceInvoiceDocumentService(IFinanceInvoiceGenerationService invoiceGenerationService)
    {
        _invoiceGenerationService = invoiceGenerationService;
    }

    public async Task<FinanceDocumentBytesResult> GeneratePartnerInvoiceAsync(
        FinanceInvoiceRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await _invoiceGenerationService.GeneratePartnerInvoiceOnDemandAsync(
            request,
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
}
