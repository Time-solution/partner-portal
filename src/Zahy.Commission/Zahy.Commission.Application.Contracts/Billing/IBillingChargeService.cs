using System;
using System.Threading;
using System.Threading.Tasks;

namespace Zahy.Commission;

public interface IBillingChargeService
{
    Task<BillingChargeResult> ChargeAsync(
        BillingChargeRequest request,
        CancellationToken cancellationToken = default);

    Task<BillingChargeResult> ChargeCommissionAccrualAsync(
        CommissionLedgerAccrualResult accrual,
        Guid partnerId,
        Guid? tenantId,
        string currency,
        CancellationToken cancellationToken = default);

    Task<BillingChargeResult> ChargeActivationIfConfiguredAsync(
        Guid partnerId,
        Guid? tenantId,
        CancellationToken cancellationToken = default);
}

public sealed class BillingChargeRequest
{
    public Guid PartnerId { get; init; }

    public BillingChargeTarget ChargeTarget { get; init; } = BillingChargeTarget.Partner;

    public Guid? TenantId { get; init; }

    public BillingChargeKind Kind { get; init; }

    public decimal Amount { get; init; }

    public string Currency { get; init; } = CommissionConsts.DefaultCurrency;

    public string IdempotencyKey { get; init; } = string.Empty;

    public string? PeriodKey { get; init; }

    public Guid? CommissionLedgerEntryId { get; init; }

    public string? Description { get; init; }
}

public sealed class BillingChargeResult
{
    public Guid ChargeId { get; init; }

    public bool IsNew { get; init; }

    public BillingChargeKind Kind { get; init; }

    public BillingChargeTarget ChargeTarget { get; init; }

    public decimal Amount { get; init; }

    public string IdempotencyKey { get; init; } = string.Empty;
}
