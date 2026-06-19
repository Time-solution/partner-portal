using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.Uow;
using Volo.Abp.Users;
using Zahy.Commission;

namespace Zahy.Finance;

public class FinanceAccountStatusService : ApplicationService, IFinanceAccountStatusService
{
    private readonly IRepository<PartnerFinancialAccount, Guid> _partnerAccountRepository;
    private readonly IRepository<MerchantAccount, Guid> _merchantAccountRepository;
    private readonly IRepository<FinanceAccountStatusAudit, Guid> _auditRepository;
    private readonly IGuidGenerator _guidGenerator;

    public FinanceAccountStatusService(
        IRepository<PartnerFinancialAccount, Guid> partnerAccountRepository,
        IRepository<MerchantAccount, Guid> merchantAccountRepository,
        IRepository<FinanceAccountStatusAudit, Guid> auditRepository,
        IGuidGenerator guidGenerator)
    {
        _partnerAccountRepository = partnerAccountRepository;
        _merchantAccountRepository = merchantAccountRepository;
        _auditRepository = auditRepository;
        _guidGenerator = guidGenerator;
    }

    [UnitOfWork]
    public virtual async Task ChangePartnerStatusAsync(
        FinanceAccountStatusChangeRequest request,
        CancellationToken cancellationToken = default)
    {
        var queryable = await _partnerAccountRepository.GetQueryableAsync();
        var account = queryable.FirstOrDefault(x => x.PartnerId == request.EntityId);
        if (account == null)
        {
            throw new BusinessException(FinanceErrorCodes.AccountNotFound)
                .WithData("PartnerId", request.EntityId);
        }

        await ApplyTransitionAsync(
            account,
            FinanceAccountKind.Partner,
            request.EntityId,
            request.TargetStatus,
            request.Reason,
            cancellationToken);
    }

    [UnitOfWork]
    public virtual async Task ChangeMerchantStatusAsync(
        FinanceAccountStatusChangeRequest request,
        CancellationToken cancellationToken = default)
    {
        var queryable = await _merchantAccountRepository.GetQueryableAsync();
        var account = queryable.FirstOrDefault(x => x.TenantId == request.EntityId);
        if (account == null)
        {
            throw new BusinessException(FinanceErrorCodes.AccountNotFound)
                .WithData("TenantId", request.EntityId);
        }

        await ApplyTransitionAsync(
            account,
            FinanceAccountKind.Merchant,
            request.EntityId,
            request.TargetStatus,
            request.Reason,
            cancellationToken);
    }

    private async Task ApplyTransitionAsync(
        PartnerFinancialAccount account,
        FinanceAccountKind accountKind,
        Guid entityId,
        FinanceAccountStatus targetStatus,
        string? reason,
        CancellationToken cancellationToken)
    {
        var fromStatus = account.Status;
        account.ApplyStatusTransition(targetStatus);
        await _partnerAccountRepository.UpdateAsync(account, autoSave: true, cancellationToken: cancellationToken);
        await InsertAuditAsync(accountKind, account.Id, entityId, fromStatus, targetStatus, reason, cancellationToken);
    }

    private async Task ApplyTransitionAsync(
        MerchantAccount account,
        FinanceAccountKind accountKind,
        Guid entityId,
        FinanceAccountStatus targetStatus,
        string? reason,
        CancellationToken cancellationToken)
    {
        var fromStatus = account.Status;
        account.ApplyStatusTransition(targetStatus);
        await _merchantAccountRepository.UpdateAsync(account, autoSave: true, cancellationToken: cancellationToken);
        await InsertAuditAsync(accountKind, account.Id, entityId, fromStatus, targetStatus, reason, cancellationToken);
    }

    private async Task InsertAuditAsync(
        FinanceAccountKind accountKind,
        Guid accountId,
        Guid entityId,
        FinanceAccountStatus fromStatus,
        FinanceAccountStatus toStatus,
        string? reason,
        CancellationToken cancellationToken)
    {
        var auditId = _guidGenerator.Create();
        var audit = FinanceAccountStatusAudit.Record(
            auditId,
            accountKind,
            accountId,
            entityId,
            fromStatus,
            toStatus,
            Clock.Now,
            CurrentUser.Id,
            reason);

        await _auditRepository.InsertAsync(audit, autoSave: true, cancellationToken: cancellationToken);
        FinanceAccountStatusAuditLog.LogTransition(
            Logger,
            auditId,
            accountKind,
            accountId,
            fromStatus,
            toStatus);
    }
}

public class MerchantOperationalStatusService : ApplicationService, IMerchantOperationalStatusService
{
    private readonly IRepository<MerchantAccount, Guid> _merchantAccountRepository;

    public MerchantOperationalStatusService(IRepository<MerchantAccount, Guid> merchantAccountRepository)
    {
        _merchantAccountRepository = merchantAccountRepository;
    }

    public async Task<bool> IsOperationalAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var queryable = await _merchantAccountRepository.GetQueryableAsync();
        var account = queryable.FirstOrDefault(x => x.TenantId == tenantId);
        if (account == null)
        {
            throw new BusinessException(FinanceErrorCodes.AccountNotFound)
                .WithData("TenantId", tenantId);
        }

        return account.IsOperational;
    }
}
