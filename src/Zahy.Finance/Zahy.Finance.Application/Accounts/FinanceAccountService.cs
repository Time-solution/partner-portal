using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.Uow;

namespace Zahy.Finance;

public class FinanceAccountService :
    ApplicationService,
    IFinanceAccountService,
    IFinanceAccountQueryService
{
    private readonly IRepository<PartnerFinancialAccount, Guid> _partnerAccountRepository;
    private readonly IRepository<MerchantAccount, Guid> _merchantAccountRepository;
    private readonly IRepository<KycVerification, Guid> _kycVerificationRepository;
    private readonly FinancePostingIngestionService _postingIngestionService;
    private readonly FinanceAccessGuard _accessGuard;
    private readonly IGuidGenerator _guidGenerator;

    public FinanceAccountService(
        IRepository<PartnerFinancialAccount, Guid> partnerAccountRepository,
        IRepository<MerchantAccount, Guid> merchantAccountRepository,
        IRepository<KycVerification, Guid> kycVerificationRepository,
        FinancePostingIngestionService postingIngestionService,
        FinanceAccessGuard accessGuard,
        IGuidGenerator guidGenerator)
    {
        _partnerAccountRepository = partnerAccountRepository;
        _merchantAccountRepository = merchantAccountRepository;
        _kycVerificationRepository = kycVerificationRepository;
        _postingIngestionService = postingIngestionService;
        _accessGuard = accessGuard;
        _guidGenerator = guidGenerator;
    }

    [UnitOfWork]
    public virtual async Task<FinanceAccountOpenResult> OpenPartnerAccountAsync(
        Guid partnerId,
        CancellationToken cancellationToken = default)
    {
        var verification = await GetVerifiedKycAsync(KycEntityKind.Partner, partnerId, cancellationToken);
        var existing = await FindPartnerAccountAsync(partnerId, cancellationToken);
        if (existing != null)
        {
            return ToOpenResult(existing, FinanceAccountKind.Partner, isNew: false);
        }

        var account = PartnerFinancialAccount.Open(
            _guidGenerator.Create(),
            partnerId,
            verification.Id,
            Clock.Now);

        await _partnerAccountRepository.InsertAsync(account, autoSave: true, cancellationToken: cancellationToken);
        return ToOpenResult(account, FinanceAccountKind.Partner, isNew: true);
    }

    [UnitOfWork]
    public virtual async Task<FinanceAccountOpenResult> OpenMerchantAccountAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var verification = await GetVerifiedKycAsync(KycEntityKind.Merchant, tenantId, cancellationToken);
        var existing = await FindMerchantAccountAsync(tenantId, cancellationToken);
        if (existing != null)
        {
            return ToOpenResult(existing, FinanceAccountKind.Merchant, isNew: false);
        }

        var account = MerchantAccount.Open(
            _guidGenerator.Create(),
            tenantId,
            verification.Id,
            Clock.Now);

        await _merchantAccountRepository.InsertAsync(account, autoSave: true, cancellationToken: cancellationToken);
        return ToOpenResult(account, FinanceAccountKind.Merchant, isNew: true);
    }

    public Task EnsureCanAccessPartnerAccountAsync(
        Guid partnerId,
        CancellationToken cancellationToken = default) =>
        _accessGuard.EnsureCanAccessPartnerAccountAsync(partnerId);

    public Task EnsureCanAccessMerchantAccountAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public async Task<FinanceAccountBalanceDto> GetPartnerBalanceAsync(
        Guid partnerId,
        CancellationToken cancellationToken = default)
    {
        await _accessGuard.EnsureCanAccessPartnerAccountAsync(partnerId);

        var account = await FindPartnerAccountAsync(partnerId, cancellationToken);
        if (account == null)
        {
            throw new BusinessException(FinanceErrorCodes.AccountNotFound)
                .WithData("PartnerId", partnerId);
        }

        var balance = await _postingIngestionService.SumPostingsForAccountAsync(
            FinanceAccountKind.Partner,
            account.Id,
            cancellationToken);

        return new FinanceAccountBalanceDto
        {
            AccountId = account.Id,
            AccountKind = FinanceAccountKind.Partner,
            Balance = balance,
            Currency = FinanceConsts.DefaultCurrency,
            Status = account.Status
        };
    }

    public async Task<FinanceAccountBalanceDto> GetMerchantBalanceAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var account = await FindMerchantAccountAsync(tenantId, cancellationToken);
        if (account == null)
        {
            throw new BusinessException(FinanceErrorCodes.AccountNotFound)
                .WithData("TenantId", tenantId);
        }

        var balance = await _postingIngestionService.SumPostingsForAccountAsync(
            FinanceAccountKind.Merchant,
            account.Id,
            cancellationToken);

        return new FinanceAccountBalanceDto
        {
            AccountId = account.Id,
            AccountKind = FinanceAccountKind.Merchant,
            Balance = balance,
            Currency = FinanceConsts.DefaultCurrency,
            Status = account.Status
        };
    }

    private async Task<KycVerification> GetVerifiedKycAsync(
        KycEntityKind entityKind,
        Guid entityId,
        CancellationToken cancellationToken)
    {
        var queryable = await _kycVerificationRepository.GetQueryableAsync();
        var verification = queryable
            .Where(x =>
                x.EntityKind == entityKind &&
                x.EntityId == entityId &&
                x.Status == KycVerificationStatus.Verified)
            .OrderByDescending(x => x.VerifiedAt)
            .FirstOrDefault();

        if (verification == null)
        {
            throw new BusinessException(FinanceErrorCodes.KycNotVerified)
                .WithData("EntityKind", entityKind.ToString())
                .WithData("EntityId", entityId);
        }

        return verification;
    }

    private async Task<PartnerFinancialAccount?> FindPartnerAccountAsync(
        Guid partnerId,
        CancellationToken cancellationToken)
    {
        var queryable = await _partnerAccountRepository.GetQueryableAsync();
        return queryable.FirstOrDefault(x => x.PartnerId == partnerId);
    }

    private async Task<MerchantAccount?> FindMerchantAccountAsync(
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var queryable = await _merchantAccountRepository.GetQueryableAsync();
        return queryable.FirstOrDefault(x => x.TenantId == tenantId);
    }

    private static FinanceAccountOpenResult ToOpenResult(
        PartnerFinancialAccount account,
        FinanceAccountKind accountKind,
        bool isNew) =>
        new()
        {
            AccountId = account.Id,
            AccountKind = accountKind,
            IsNew = isNew,
            Status = account.Status
        };

    private static FinanceAccountOpenResult ToOpenResult(
        MerchantAccount account,
        FinanceAccountKind accountKind,
        bool isNew) =>
        new()
        {
            AccountId = account.Id,
            AccountKind = accountKind,
            IsNew = isNew,
            Status = account.Status
        };
}
