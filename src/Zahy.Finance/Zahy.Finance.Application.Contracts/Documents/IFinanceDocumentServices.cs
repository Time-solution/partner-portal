using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Zahy.Finance;

public interface IFinancePostingReadService
{
    Task<FinancePostingLedgerSnapshot> GetPartnerLedgerAsync(
        Guid partnerId,
        DateTime? periodFrom = null,
        DateTime? periodTo = null,
        CancellationToken cancellationToken = default);

    Task<FinancePostingLedgerSnapshot> GetMerchantLedgerAsync(
        Guid tenantId,
        DateTime? periodFrom = null,
        DateTime? periodTo = null,
        CancellationToken cancellationToken = default);
}

public interface IFinanceDocumentExportService
{
    Task<FinanceDocumentBytesResult> ExportPartnerPostingsAsync(
        FinanceDocumentExportRequest request,
        CancellationToken cancellationToken = default);

    Task<FinanceDocumentBytesResult> ExportMerchantPostingsAsync(
        FinanceDocumentExportRequest request,
        CancellationToken cancellationToken = default);
}

public interface IFinanceStatementDocumentService
{
    Task<FinanceDocumentBytesResult> GeneratePartnerStatementAsync(
        FinanceStatementRequest request,
        CancellationToken cancellationToken = default);

    Task<FinanceDocumentBytesResult> GenerateMerchantStatementAsync(
        FinanceStatementRequest request,
        CancellationToken cancellationToken = default);
}

public interface IFinanceInvoiceDocumentService
{
    Task<FinanceDocumentBytesResult> GeneratePartnerInvoiceAsync(
        FinanceInvoiceRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class FinancePostingLedgerSnapshot
{
    public Guid AccountId { get; init; }

    public FinanceAccountKind AccountKind { get; init; }

    public decimal PostingSum { get; init; }

    public string Currency { get; init; } = FinanceConsts.DefaultCurrency;

    public IReadOnlyList<FinancePostingRowDto> Postings { get; init; } = Array.Empty<FinancePostingRowDto>();
}

public sealed class FinancePostingRowDto
{
    public Guid PostingId { get; init; }

    public DateTime PostedAt { get; init; }

    public decimal PostingAmount { get; init; }

    public FinancePostingSourceModule SourceModule { get; init; }

    public string SourceType { get; init; } = string.Empty;

    public string SourceId { get; init; } = string.Empty;

    public string? Description { get; init; }
}

public sealed class FinanceDocumentExportRequest
{
    public Guid EntityId { get; init; }

    public FinanceExportFormat Format { get; init; } = FinanceExportFormat.Csv;

    public DateTime? PeriodFrom { get; init; }

    public DateTime? PeriodTo { get; init; }
}

public sealed class FinanceStatementRequest
{
    public Guid EntityId { get; init; }

    public DateTime? PeriodFrom { get; init; }

    public DateTime? PeriodTo { get; init; }
}

public sealed class FinanceInvoiceRequest
{
    public Guid PartnerId { get; init; }

    public DateTime? PeriodFrom { get; init; }

    public DateTime? PeriodTo { get; init; }
}

public sealed class FinanceDocumentBytesResult
{
    public byte[] Content { get; init; } = Array.Empty<byte>();

    public string ContentType { get; init; } = string.Empty;

    public string FileName { get; init; } = string.Empty;

    public decimal PostingSum { get; init; }

    public FinanceDocumentKind DocumentKind { get; init; }
}
