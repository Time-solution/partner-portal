using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;

namespace Zahy.Finance;

public interface IFinancePartnerPortalAppService
{
    Task<FinancePortalAccountDto> GetAccountAsync(CancellationToken cancellationToken = default);

    Task<PagedResultDto<FinancePortalPostingRowDto>> GetPostingsAsync(
        FinancePortalPostingsRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FinancePortalDocumentListItemDto>> GetDocumentsAsync(
        CancellationToken cancellationToken = default);

    Task<FinancePortalDownloadDto> ExportPostingsAsync(
        FinancePortalExportRequest request,
        CancellationToken cancellationToken = default);

    Task<FinancePortalDownloadDto> DownloadDocumentAsync(
        Guid documentId,
        CancellationToken cancellationToken = default);
}

public interface IFinanceMerchantPortalAppService
{
    Task<FinancePortalAccountDto> GetAccountAsync(CancellationToken cancellationToken = default);

    Task<PagedResultDto<FinancePortalPostingRowDto>> GetPostingsAsync(
        FinancePortalPostingsRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FinancePortalDocumentListItemDto>> GetDocumentsAsync(
        CancellationToken cancellationToken = default);

    Task<FinancePortalDownloadDto> ExportPostingsAsync(
        FinancePortalExportRequest request,
        CancellationToken cancellationToken = default);

    Task<FinancePortalDownloadDto> DownloadDocumentAsync(
        Guid documentId,
        CancellationToken cancellationToken = default);
}

public sealed class FinancePortalAccountDto
{
    public Guid AccountId { get; init; }

    public FinanceAccountKind AccountKind { get; init; }

    public decimal Balance { get; init; }

    public string Currency { get; init; } = FinanceConsts.DefaultCurrency;

    public FinanceAccountStatus Status { get; init; }

    public KycVerificationStatus KycStatus { get; init; }

    public bool IsOperational { get; init; }
}

public sealed class FinancePortalPostingsRequest : PagedResultRequestDto
{
    public DateTime? From { get; init; }

    public DateTime? To { get; init; }
}

public sealed class FinancePortalPostingRowDto
{
    public Guid PostingId { get; init; }

    public DateTime PostedAt { get; init; }

    public decimal PostingAmount { get; init; }

    public decimal RunningBalance { get; init; }

    public FinancePostingSourceModule SourceModule { get; init; }

    public string SourceType { get; init; } = string.Empty;

    public string SourceId { get; init; } = string.Empty;

    public string? Description { get; init; }
}

public sealed class FinancePortalDocumentListItemDto
{
    public Guid DocumentId { get; init; }

    public FinanceDocumentKind DocumentKind { get; init; }

    public string InvoiceNumber { get; init; } = string.Empty;

    public decimal PostingSum { get; init; }

    public DateTime GeneratedAt { get; init; }
}

public sealed class FinancePortalExportRequest
{
    public FinanceExportFormat Format { get; init; } = FinanceExportFormat.Csv;

    public DateTime? From { get; init; }

    public DateTime? To { get; init; }
}

public sealed class FinancePortalDownloadDto
{
    public byte[] Content { get; init; } = Array.Empty<byte>();

    public string ContentType { get; init; } = string.Empty;

    public string FileName { get; init; } = string.Empty;

    public decimal PostingSum { get; init; }
}
