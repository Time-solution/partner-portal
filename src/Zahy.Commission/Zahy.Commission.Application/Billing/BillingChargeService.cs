using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.Uow;

namespace Zahy.Commission;

public class BillingChargeService : ApplicationService, IBillingChargeService
{
    private readonly IRepository<BillingCharge, Guid> _chargeRepository;
    private readonly IRepository<PartnerBillingProfile, Guid> _profileRepository;
    private readonly IGuidGenerator _guidGenerator;

    public BillingChargeService(
        IRepository<BillingCharge, Guid> chargeRepository,
        IRepository<PartnerBillingProfile, Guid> profileRepository,
        IGuidGenerator guidGenerator)
    {
        _chargeRepository = chargeRepository;
        _profileRepository = profileRepository;
        _guidGenerator = guidGenerator;
    }

    [UnitOfWork]
    public virtual async Task<BillingChargeResult> ChargeAsync(
        BillingChargeRequest request,
        CancellationToken cancellationToken = default)
    {
        var existing = await FindByIdempotencyKeyAsync(request.IdempotencyKey, cancellationToken);
        if (existing != null)
        {
            return ToResult(existing, isNew: false);
        }

        var charge = BillingCharge.Create(
            _guidGenerator.Create(),
            request.PartnerId,
            request.TenantId,
            request.Kind,
            request.Amount,
            request.IdempotencyKey,
            Clock.Now,
            request.Currency,
            request.PeriodKey,
            request.CommissionLedgerEntryId,
            request.Description);

        await _chargeRepository.InsertAsync(charge, autoSave: true, cancellationToken: cancellationToken);
        return ToResult(charge, isNew: true);
    }

    [UnitOfWork]
    public virtual async Task<BillingChargeResult> ChargeCommissionAccrualAsync(
        CommissionLedgerAccrualResult accrual,
        Guid partnerId,
        Guid? tenantId,
        string currency,
        CancellationToken cancellationToken = default)
    {
        if (!accrual.IsNew || accrual.ComputedCommission <= 0)
        {
            var existingKey = BillingIdempotency.BuildCommissionTransactionKey(partnerId, accrual.EntryId);
            var existing = await FindByIdempotencyKeyAsync(existingKey, cancellationToken);
            if (existing != null)
            {
                return ToResult(existing, isNew: false);
            }

            return new BillingChargeResult
            {
                ChargeId = accrual.EntryId,
                IsNew = false,
                Kind = BillingChargeKind.Transaction,
                Amount = accrual.ComputedCommission,
                IdempotencyKey = existingKey
            };
        }

        return await ChargeAsync(new BillingChargeRequest
        {
            PartnerId = partnerId,
            TenantId = tenantId,
            Kind = BillingChargeKind.Transaction,
            Amount = accrual.ComputedCommission,
            Currency = currency,
            IdempotencyKey = BillingIdempotency.BuildCommissionTransactionKey(partnerId, accrual.EntryId),
            CommissionLedgerEntryId = accrual.EntryId,
            Description = $"Commission accrual {accrual.EntryId:N}"
        }, cancellationToken);
    }

    [UnitOfWork]
    public virtual async Task<BillingChargeResult> ChargeActivationIfConfiguredAsync(
        Guid partnerId,
        Guid? tenantId,
        CancellationToken cancellationToken = default)
    {
        var profile = await FindProfileAsync(partnerId, cancellationToken);
        if (profile == null || !profile.IsActive || profile.ActivationFeeAmount <= 0)
        {
            return new BillingChargeResult
            {
                IsNew = false,
                Kind = BillingChargeKind.ActivationFee,
                Amount = 0m,
                IdempotencyKey = BillingIdempotency.BuildActivationKey(partnerId)
            };
        }

        return await ChargeAsync(new BillingChargeRequest
        {
            PartnerId = partnerId,
            TenantId = tenantId,
            Kind = BillingChargeKind.ActivationFee,
            Amount = profile.ActivationFeeAmount,
            Currency = profile.Currency,
            IdempotencyKey = BillingIdempotency.BuildActivationKey(partnerId),
            Description = "Partner activation fee"
        }, cancellationToken);
    }

    private async Task<PartnerBillingProfile?> FindProfileAsync(
        Guid partnerId,
        CancellationToken cancellationToken)
    {
        var queryable = await _profileRepository.GetQueryableAsync();
        return queryable.FirstOrDefault(x => x.PartnerId == partnerId && x.IsActive);
    }

    private async Task<BillingCharge?> FindByIdempotencyKeyAsync(
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        var queryable = await _chargeRepository.GetQueryableAsync();
        return queryable.FirstOrDefault(x => x.IdempotencyKey == idempotencyKey);
    }

    private static BillingChargeResult ToResult(BillingCharge charge, bool isNew) =>
        new()
        {
            ChargeId = charge.Id,
            IsNew = isNew,
            Kind = charge.Kind,
            Amount = charge.Amount,
            IdempotencyKey = charge.IdempotencyKey
        };
}
