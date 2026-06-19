using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;
using Volo.Abp.Domain.Repositories;
using Zahy.Identity.Permissions;

namespace Zahy.Finance;

[Authorize]
public class FinancePartnerPortalAppService : ApplicationService, IFinancePartnerPortalAppService
{
    private readonly FinancePortalReadService _portalReadService;
    private readonly FinanceDocumentDownloadService _documentDownloadService;
    private readonly FinanceAccessGuard _accessGuard;

    public FinancePartnerPortalAppService(
        FinancePortalReadService portalReadService,
        FinanceDocumentDownloadService documentDownloadService,
        FinanceAccessGuard accessGuard)
    {
        _portalReadService = portalReadService;
        _documentDownloadService = documentDownloadService;
        _accessGuard = accessGuard;
    }

    [Authorize(ZahyPermissions.Payouts.Read)]
    public Task<FinancePortalAccountDto> GetAccountAsync(CancellationToken cancellationToken = default) =>
        _portalReadService.GetPartnerAccountAsync(cancellationToken);

    [Authorize(ZahyPermissions.Payouts.Read)]
    public Task<PagedResultDto<FinancePortalPostingRowDto>> GetPostingsAsync(
        FinancePortalPostingsRequest request,
        CancellationToken cancellationToken = default) =>
        _portalReadService.GetPartnerPostingsAsync(request, cancellationToken);

    [Authorize(ZahyPermissions.Payouts.Read)]
    public Task<IReadOnlyList<FinancePortalDocumentListItemDto>> GetDocumentsAsync(
        CancellationToken cancellationToken = default) =>
        _portalReadService.GetPartnerDocumentsAsync(cancellationToken);

    [Authorize(ZahyPermissions.Payouts.Read)]
    public async Task<FinancePortalDownloadDto> ExportPostingsAsync(
        FinancePortalExportRequest request,
        CancellationToken cancellationToken = default)
    {
        var partnerId = await _accessGuard.GetRequiredPartnerIdAsync();
        return await _documentDownloadService.ExportPartnerPostingsAsync(partnerId, request, cancellationToken);
    }

    [Authorize(ZahyPermissions.Payouts.Read)]
    public Task<FinancePortalDownloadDto> DownloadDocumentAsync(
        Guid documentId,
        CancellationToken cancellationToken = default) =>
        _documentDownloadService.DownloadPartnerDocumentAsync(documentId, cancellationToken);
}

[Authorize]
public class FinanceMerchantPortalAppService : ApplicationService, IFinanceMerchantPortalAppService
{
    private readonly FinancePortalReadService _portalReadService;
    private readonly FinanceDocumentDownloadService _documentDownloadService;
    private readonly FinanceAccessGuard _accessGuard;

    public FinanceMerchantPortalAppService(
        FinancePortalReadService portalReadService,
        FinanceDocumentDownloadService documentDownloadService,
        FinanceAccessGuard accessGuard)
    {
        _portalReadService = portalReadService;
        _documentDownloadService = documentDownloadService;
        _accessGuard = accessGuard;
    }

    [Authorize(ZahyPermissions.Payouts.Read)]
    public Task<FinancePortalAccountDto> GetAccountAsync(CancellationToken cancellationToken = default) =>
        _portalReadService.GetMerchantAccountAsync(cancellationToken);

    [Authorize(ZahyPermissions.Payouts.Read)]
    public Task<PagedResultDto<FinancePortalPostingRowDto>> GetPostingsAsync(
        FinancePortalPostingsRequest request,
        CancellationToken cancellationToken = default) =>
        _portalReadService.GetMerchantPostingsAsync(request, cancellationToken);

    [Authorize(ZahyPermissions.Payouts.Read)]
    public Task<IReadOnlyList<FinancePortalDocumentListItemDto>> GetDocumentsAsync(
        CancellationToken cancellationToken = default) =>
        _portalReadService.GetMerchantDocumentsAsync(cancellationToken);

    [Authorize(ZahyPermissions.Payouts.Read)]
    public async Task<FinancePortalDownloadDto> ExportPostingsAsync(
        FinancePortalExportRequest request,
        CancellationToken cancellationToken = default)
    {
        var tenantId = await _accessGuard.GetRequiredTenantIdAsync();
        return await _documentDownloadService.ExportMerchantPostingsAsync(tenantId, request, cancellationToken);
    }

    [Authorize(ZahyPermissions.Payouts.Read)]
    public Task<FinancePortalDownloadDto> DownloadDocumentAsync(
        Guid documentId,
        CancellationToken cancellationToken = default) =>
        _documentDownloadService.DownloadMerchantDocumentAsync(documentId, cancellationToken);
}

public class FinancePortalReadService : ApplicationService
{
    private readonly IFinanceAccountQueryService _accountQueryService;
    private readonly IFinancePostingReadService _postingReadService;
    private readonly IRepository<FinanceDocument, Guid> _documentRepository;
    private readonly IRepository<KycVerification, Guid> _kycVerificationRepository;
    private readonly IMerchantOperationalStatusService _operationalStatusService;
    private readonly FinanceAccessGuard _accessGuard;

    public FinancePortalReadService(
        IFinanceAccountQueryService accountQueryService,
        IFinancePostingReadService postingReadService,
        IRepository<FinanceDocument, Guid> documentRepository,
        IRepository<KycVerification, Guid> kycVerificationRepository,
        IMerchantOperationalStatusService operationalStatusService,
        FinanceAccessGuard accessGuard)
    {
        _accountQueryService = accountQueryService;
        _postingReadService = postingReadService;
        _documentRepository = documentRepository;
        _kycVerificationRepository = kycVerificationRepository;
        _operationalStatusService = operationalStatusService;
        _accessGuard = accessGuard;
    }

    public async Task<FinancePortalAccountDto> GetPartnerAccountAsync(CancellationToken cancellationToken)
    {
        var partnerId = await _accessGuard.GetRequiredPartnerIdAsync();
        var balance = await _accountQueryService.GetPartnerBalanceAsync(partnerId, cancellationToken);
        var kycStatus = await ResolveLatestKycStatusAsync(KycEntityKind.Partner, partnerId, cancellationToken);

        return new FinancePortalAccountDto
        {
            AccountId = balance.AccountId,
            AccountKind = balance.AccountKind,
            Balance = balance.Balance,
            Currency = balance.Currency,
            Status = balance.Status,
            KycStatus = kycStatus,
            IsOperational = balance.Status == FinanceAccountStatus.Active
        };
    }

    public async Task<FinancePortalAccountDto> GetMerchantAccountAsync(CancellationToken cancellationToken)
    {
        var tenantId = await _accessGuard.GetRequiredTenantIdAsync();
        await _accessGuard.EnsureCanAccessMerchantAccountAsync(tenantId);
        var balance = await _accountQueryService.GetMerchantBalanceAsync(tenantId, cancellationToken);
        var kycStatus = await ResolveLatestKycStatusAsync(KycEntityKind.Merchant, tenantId, cancellationToken);
        var isOperational = await _operationalStatusService.IsOperationalAsync(tenantId, cancellationToken);

        return new FinancePortalAccountDto
        {
            AccountId = balance.AccountId,
            AccountKind = balance.AccountKind,
            Balance = balance.Balance,
            Currency = balance.Currency,
            Status = balance.Status,
            KycStatus = kycStatus,
            IsOperational = isOperational
        };
    }

    public async Task<PagedResultDto<FinancePortalPostingRowDto>> GetPartnerPostingsAsync(
        FinancePortalPostingsRequest request,
        CancellationToken cancellationToken)
    {
        var partnerId = await _accessGuard.GetRequiredPartnerIdAsync();
        var ledger = await _postingReadService.GetPartnerLedgerAsync(
            partnerId,
            request.From,
            request.To,
            cancellationToken);

        return BuildPostingPage(ledger.Postings, request);
    }

    public async Task<PagedResultDto<FinancePortalPostingRowDto>> GetMerchantPostingsAsync(
        FinancePortalPostingsRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = await _accessGuard.GetRequiredTenantIdAsync();
        var ledger = await _postingReadService.GetMerchantLedgerAsync(
            tenantId,
            request.From,
            request.To,
            cancellationToken);

        return BuildPostingPage(ledger.Postings, request);
    }

    public async Task<IReadOnlyList<FinancePortalDocumentListItemDto>> GetPartnerDocumentsAsync(
        CancellationToken cancellationToken)
    {
        var partnerId = await _accessGuard.GetRequiredPartnerIdAsync();
        return await ListDocumentsAsync(FinanceAccountKind.Partner, partnerId, cancellationToken);
    }

    public async Task<IReadOnlyList<FinancePortalDocumentListItemDto>> GetMerchantDocumentsAsync(
        CancellationToken cancellationToken)
    {
        var tenantId = await _accessGuard.GetRequiredTenantIdAsync();
        return await ListDocumentsAsync(FinanceAccountKind.Merchant, tenantId, cancellationToken);
    }

    private async Task<KycVerificationStatus> ResolveLatestKycStatusAsync(
        KycEntityKind entityKind,
        Guid entityId,
        CancellationToken cancellationToken)
    {
        var queryable = await _kycVerificationRepository.GetQueryableAsync();
        var verification = queryable
            .Where(x => x.EntityKind == entityKind && x.EntityId == entityId)
            .OrderByDescending(x => x.VerifiedAt ?? x.ReviewedAt)
            .FirstOrDefault();

        return verification?.Status ?? KycVerificationStatus.Submitted;
    }

    private async Task<IReadOnlyList<FinancePortalDocumentListItemDto>> ListDocumentsAsync(
        FinanceAccountKind accountKind,
        Guid entityId,
        CancellationToken cancellationToken)
    {
        var queryable = await _documentRepository.GetQueryableAsync();
        return queryable
            .Where(x => x.AccountKind == accountKind && x.EntityId == entityId)
            .OrderByDescending(x => x.GeneratedAt)
            .Select(x => new FinancePortalDocumentListItemDto
            {
                DocumentId = x.Id,
                DocumentKind = x.DocumentKind,
                InvoiceNumber = x.InvoiceNumber,
                PostingSum = x.PostingSum,
                GeneratedAt = x.GeneratedAt
            })
            .ToList();
    }

    private static PagedResultDto<FinancePortalPostingRowDto> BuildPostingPage(
        IReadOnlyList<FinancePostingRowDto> postings,
        FinancePortalPostingsRequest request)
    {
        decimal running = 0m;
        var rows = new List<FinancePortalPostingRowDto>(postings.Count);

        foreach (var posting in postings)
        {
            running = FinanceMoney.RoundPosting(running + posting.PostingAmount);
            rows.Add(new FinancePortalPostingRowDto
            {
                PostingId = posting.PostingId,
                PostedAt = posting.PostedAt,
                PostingAmount = posting.PostingAmount,
                RunningBalance = running,
                SourceModule = posting.SourceModule,
                SourceType = posting.SourceType,
                SourceId = posting.SourceId,
                Description = posting.Description
            });
        }

        var totalCount = rows.Count;
        var page = rows
            .Skip(request.SkipCount)
            .Take(request.MaxResultCount)
            .ToList();

        return new PagedResultDto<FinancePortalPostingRowDto>(totalCount, page);
    }
}

public class FinanceDocumentDownloadService : ApplicationService
{
    private readonly IFinanceDocumentExportService _exportService;
    private readonly IFinanceInvoiceGenerationService _invoiceGenerationService;
    private readonly IFinanceStatementDocumentService _statementService;
    private readonly IRepository<FinanceDocument, Guid> _documentRepository;
    private readonly FinanceAccessGuard _accessGuard;

    public FinanceDocumentDownloadService(
        IFinanceDocumentExportService exportService,
        IFinanceInvoiceGenerationService invoiceGenerationService,
        IFinanceStatementDocumentService statementService,
        IRepository<FinanceDocument, Guid> documentRepository,
        FinanceAccessGuard accessGuard)
    {
        _exportService = exportService;
        _invoiceGenerationService = invoiceGenerationService;
        _statementService = statementService;
        _documentRepository = documentRepository;
        _accessGuard = accessGuard;
    }

    public async Task<FinancePortalDownloadDto> ExportPartnerPostingsAsync(
        Guid partnerId,
        FinancePortalExportRequest request,
        CancellationToken cancellationToken)
    {
        await _accessGuard.EnsureCanAccessPartnerAccountAsync(partnerId);
        var export = await _exportService.ExportPartnerPostingsAsync(
            new FinanceDocumentExportRequest
            {
                EntityId = partnerId,
                Format = request.Format,
                PeriodFrom = request.From,
                PeriodTo = request.To
            },
            cancellationToken);

        return MapDownload(export);
    }

    public async Task<FinancePortalDownloadDto> ExportMerchantPostingsAsync(
        Guid tenantId,
        FinancePortalExportRequest request,
        CancellationToken cancellationToken)
    {
        await _accessGuard.EnsureCanAccessMerchantAccountAsync(tenantId);
        var export = await _exportService.ExportMerchantPostingsAsync(
            new FinanceDocumentExportRequest
            {
                EntityId = tenantId,
                Format = request.Format,
                PeriodFrom = request.From,
                PeriodTo = request.To
            },
            cancellationToken);

        return MapDownload(export);
    }

    public async Task<FinancePortalDownloadDto> DownloadPartnerDocumentAsync(
        Guid documentId,
        CancellationToken cancellationToken)
    {
        var partnerId = await _accessGuard.GetRequiredPartnerIdAsync();
        var document = await LoadOwnedDocumentAsync(
            documentId,
            FinanceAccountKind.Partner,
            partnerId,
            cancellationToken);

        return await BuildDocumentDownloadAsync(document, cancellationToken);
    }

    public async Task<FinancePortalDownloadDto> DownloadMerchantDocumentAsync(
        Guid documentId,
        CancellationToken cancellationToken)
    {
        var tenantId = await _accessGuard.GetRequiredTenantIdAsync();
        var document = await LoadOwnedDocumentAsync(
            documentId,
            FinanceAccountKind.Merchant,
            tenantId,
            cancellationToken);

        return await BuildDocumentDownloadAsync(document, cancellationToken);
    }

    private async Task<FinanceDocument> LoadOwnedDocumentAsync(
        Guid documentId,
        FinanceAccountKind expectedKind,
        Guid expectedEntityId,
        CancellationToken cancellationToken)
    {
        FinanceDocument document;
        try
        {
            document = await _documentRepository.GetAsync(documentId, cancellationToken: cancellationToken);
        }
        catch (Exception)
        {
            throw new AbpAuthorizationException("Document access denied.");
        }

        if (document.AccountKind != expectedKind || document.EntityId != expectedEntityId)
        {
            throw new AbpAuthorizationException("Document access denied.");
        }

        return document;
    }

    private async Task<FinancePortalDownloadDto> BuildDocumentDownloadAsync(
        FinanceDocument document,
        CancellationToken cancellationToken)
    {
        if (document.DocumentKind == FinanceDocumentKind.Invoice)
        {
            var invoice = await _invoiceGenerationService.RegenerateInvoiceByIdempotencyAsync(
                document.AccountKind,
                document.EntityId,
                document.IdempotencyKey,
                cancellationToken);

            return new FinancePortalDownloadDto
            {
                Content = invoice.Content,
                ContentType = invoice.ContentType,
                FileName = invoice.FileName,
                PostingSum = invoice.PostingSum
            };
        }

        var statement = document.AccountKind == FinanceAccountKind.Partner
            ? await _statementService.GeneratePartnerStatementAsync(
                new FinanceStatementRequest { EntityId = document.EntityId },
                cancellationToken)
            : await _statementService.GenerateMerchantStatementAsync(
                new FinanceStatementRequest { EntityId = document.EntityId },
                cancellationToken);

        return MapDownload(statement);
    }

    private static FinancePortalDownloadDto MapDownload(FinanceDocumentBytesResult result) =>
        new()
        {
            Content = result.Content,
            ContentType = result.ContentType,
            FileName = result.FileName,
            PostingSum = result.PostingSum
        };
}
