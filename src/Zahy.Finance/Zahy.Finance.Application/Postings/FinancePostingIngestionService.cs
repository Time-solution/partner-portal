using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.Uow;
using Zahy.Commission;

namespace Zahy.Finance;

public class FinancePostingIngestionService : ApplicationService
{
    private readonly IRepository<PartnerFinancialAccount, Guid> _partnerAccountRepository;
    private readonly IRepository<MerchantAccount, Guid> _merchantAccountRepository;
    private readonly IRepository<AccountPosting, Guid> _postingRepository;
    private readonly IGuidGenerator _guidGenerator;

    public FinancePostingIngestionService(
        IRepository<PartnerFinancialAccount, Guid> partnerAccountRepository,
        IRepository<MerchantAccount, Guid> merchantAccountRepository,
        IRepository<AccountPosting, Guid> postingRepository,
        IGuidGenerator guidGenerator)
    {
        _partnerAccountRepository = partnerAccountRepository;
        _merchantAccountRepository = merchantAccountRepository;
        _postingRepository = postingRepository;
        _guidGenerator = guidGenerator;
    }

    [UnitOfWork]
    public virtual async Task IngestCommissionAccrualAsync(
        CommissionLedgerFinanceAccrualContext context,
        CancellationToken cancellationToken = default)
    {
        if (!context.IsNew)
        {
            return;
        }

        var account = await FindActivePartnerAccountAsync(context.PartnerId, cancellationToken);
        if (account == null)
        {
            Logger.LogDebug(
                "Skipping commission finance posting for partner {PartnerId} — no active account.",
                context.PartnerId);
            return;
        }

        var idempotencyKey = FinancePostingIdempotency.BuildCommissionKey(context.EntryId);
        if (await PostingExistsAsync(idempotencyKey, cancellationToken))
        {
            return;
        }

        var posting = AccountPosting.Create(
            _guidGenerator.Create(),
            FinanceAccountKind.Partner,
            account.Id,
            context.PartnerId,
            context.TenantId,
            FinancePostingSignMapper.MapCommissionAmount(context.Direction, context.ComputedCommission),
            idempotencyKey,
            Clock.Now,
            FinancePostingSourceModule.Commission,
            context.SourceType,
            context.SourceId,
            sourceRowId: context.EntryId,
            currency: context.Currency,
            description: $"Commission accrual {context.EntryId:N}");

        await _postingRepository.InsertAsync(posting, autoSave: true, cancellationToken: cancellationToken);
    }

    [UnitOfWork]
    public virtual async Task IngestBillingChargeAsync(
        BillingChargeFinanceContext context,
        CancellationToken cancellationToken = default)
    {
        if (!context.IsNew)
        {
            return;
        }

        var amount = FinancePostingSignMapper.MapBillingChargeAmount(context.Amount);
        var idempotencyKey = FinancePostingIdempotency.BuildBillingKey(context.ChargeId);
        if (await PostingExistsAsync(idempotencyKey, cancellationToken))
        {
            return;
        }

        if (context.ChargeTarget == BillingChargeTarget.Merchant)
        {
            var merchantAccount = await FindActiveMerchantAccountAsync(context.TenantId!.Value, cancellationToken);
            if (merchantAccount == null)
            {
                Logger.LogDebug(
                    "Skipping billing finance posting for tenant {TenantId} — no active merchant account.",
                    context.TenantId);
                return;
            }

            var posting = AccountPosting.Create(
                _guidGenerator.Create(),
                FinanceAccountKind.Merchant,
                merchantAccount.Id,
                context.PartnerId,
                context.TenantId,
                amount,
                idempotencyKey,
                Clock.Now,
                FinancePostingSourceModule.Billing,
                sourceType: "billing.charge",
                sourceId: context.IdempotencyKey,
                sourceRowId: context.ChargeId,
                currency: context.Currency,
                description: $"Billing charge {context.Kind} {context.ChargeId:N}");

            await _postingRepository.InsertAsync(posting, autoSave: true, cancellationToken: cancellationToken);
            return;
        }

        var partnerAccount = await FindActivePartnerAccountAsync(context.PartnerId, cancellationToken);
        if (partnerAccount == null)
        {
            Logger.LogDebug(
                "Skipping billing finance posting for partner {PartnerId} — no active account.",
                context.PartnerId);
            return;
        }

        var partnerPosting = AccountPosting.Create(
            _guidGenerator.Create(),
            FinanceAccountKind.Partner,
            partnerAccount.Id,
            context.PartnerId,
            context.TenantId,
            amount,
            idempotencyKey,
            Clock.Now,
            FinancePostingSourceModule.Billing,
            sourceType: "billing.charge",
            sourceId: context.IdempotencyKey,
            sourceRowId: context.ChargeId,
            currency: context.Currency,
            description: $"Billing charge {context.Kind} {context.ChargeId:N}");

        await _postingRepository.InsertAsync(partnerPosting, autoSave: true, cancellationToken: cancellationToken);
    }

    public async Task<decimal> SumPostingsForAccountAsync(
        FinanceAccountKind accountKind,
        Guid accountId,
        CancellationToken cancellationToken = default)
    {
        var queryable = await _postingRepository.GetQueryableAsync();
        return queryable
            .Where(x => x.AccountKind == accountKind && x.AccountId == accountId)
            .Sum(x => x.PostingAmount);
    }

    private async Task<PartnerFinancialAccount?> FindActivePartnerAccountAsync(
        Guid partnerId,
        CancellationToken cancellationToken)
    {
        var queryable = await _partnerAccountRepository.GetQueryableAsync();
        return queryable.FirstOrDefault(x =>
            x.PartnerId == partnerId &&
            x.Status == FinanceAccountStatus.Active);
    }

    private async Task<MerchantAccount?> FindActiveMerchantAccountAsync(
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var queryable = await _merchantAccountRepository.GetQueryableAsync();
        return queryable.FirstOrDefault(x =>
            x.TenantId == tenantId &&
            x.Status == FinanceAccountStatus.Active);
    }

    private async Task<bool> PostingExistsAsync(string idempotencyKey, CancellationToken cancellationToken)
    {
        var queryable = await _postingRepository.GetQueryableAsync();
        return queryable.Any(x => x.IdempotencyKey == idempotencyKey);
    }
}
