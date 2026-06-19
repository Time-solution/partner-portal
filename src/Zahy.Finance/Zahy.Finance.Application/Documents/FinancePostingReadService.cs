using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace Zahy.Finance;

public class FinancePostingReadService : ApplicationService, IFinancePostingReadService
{
    private readonly IRepository<PartnerFinancialAccount, Guid> _partnerAccountRepository;
    private readonly IRepository<MerchantAccount, Guid> _merchantAccountRepository;
    private readonly IRepository<AccountPosting, Guid> _postingRepository;
    private readonly FinanceAccessGuard _accessGuard;

    public FinancePostingReadService(
        IRepository<PartnerFinancialAccount, Guid> partnerAccountRepository,
        IRepository<MerchantAccount, Guid> merchantAccountRepository,
        IRepository<AccountPosting, Guid> postingRepository,
        FinanceAccessGuard accessGuard)
    {
        _partnerAccountRepository = partnerAccountRepository;
        _merchantAccountRepository = merchantAccountRepository;
        _postingRepository = postingRepository;
        _accessGuard = accessGuard;
    }

    public async Task<FinancePostingLedgerSnapshot> GetPartnerLedgerAsync(
        Guid partnerId,
        DateTime? periodFrom = null,
        DateTime? periodTo = null,
        CancellationToken cancellationToken = default)
    {
        await _accessGuard.EnsureCanAccessPartnerAccountAsync(partnerId);
        var account = await FindPartnerAccountAsync(partnerId, cancellationToken);
        return await BuildSnapshotAsync(
            FinanceAccountKind.Partner,
            account.Id,
            periodFrom,
            periodTo,
            cancellationToken);
    }

    public async Task<FinancePostingLedgerSnapshot> GetMerchantLedgerAsync(
        Guid tenantId,
        DateTime? periodFrom = null,
        DateTime? periodTo = null,
        CancellationToken cancellationToken = default)
    {
        await _accessGuard.EnsureCanAccessMerchantAccountAsync(tenantId);
        var account = await FindMerchantAccountAsync(tenantId, cancellationToken);
        return await BuildSnapshotAsync(
            FinanceAccountKind.Merchant,
            account.Id,
            periodFrom,
            periodTo,
            cancellationToken);
    }

    internal async Task<FinancePostingLedgerSnapshot> BuildSnapshotAsync(
        FinanceAccountKind accountKind,
        Guid accountId,
        DateTime? periodFrom,
        DateTime? periodTo,
        CancellationToken cancellationToken)
    {
        var queryable = await _postingRepository.GetQueryableAsync();
        var filtered = queryable
            .Where(x => x.AccountKind == accountKind && x.AccountId == accountId);

        if (periodFrom != null)
        {
            filtered = filtered.Where(x => x.PostedAt >= periodFrom);
        }

        if (periodTo != null)
        {
            filtered = filtered.Where(x => x.PostedAt <= periodTo);
        }

        var rows = filtered
            .OrderBy(x => x.PostedAt)
            .Select(x => new FinancePostingRowDto
            {
                PostingId = x.Id,
                PostedAt = x.PostedAt,
                PostingAmount = x.PostingAmount,
                SourceModule = x.SourceModule,
                SourceType = x.SourceType,
                SourceId = x.SourceId,
                Description = x.Description
            })
            .ToList();

        var postingSum = filtered.Sum(x => x.PostingAmount);

        return new FinancePostingLedgerSnapshot
        {
            AccountId = accountId,
            AccountKind = accountKind,
            PostingSum = postingSum,
            Currency = FinanceConsts.DefaultCurrency,
            Postings = rows
        };
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
