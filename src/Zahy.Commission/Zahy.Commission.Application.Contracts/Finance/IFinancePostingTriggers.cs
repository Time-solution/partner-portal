using System;
using System.Threading;
using System.Threading.Tasks;
using Zahy.Commission;

namespace Zahy.Commission;

/// <summary>In-process finance posting trigger — replaced by Zahy.Finance when loaded.</summary>
public interface ICommissionLedgerFinanceTrigger
{
    Task NotifyAccruedAsync(
        CommissionLedgerFinanceAccrualContext context,
        CancellationToken cancellationToken = default);
}

public sealed class CommissionLedgerFinanceAccrualContext
{
    public Guid EntryId { get; init; }

    public bool IsNew { get; init; }

    public Guid PartnerId { get; init; }

    public Guid? TenantId { get; init; }

    public decimal ComputedCommission { get; init; }

    public CommissionDirection Direction { get; init; }

    public CommissionEntryKind EntryKind { get; init; }

    public string Currency { get; init; } = CommissionConsts.DefaultCurrency;

    public string SourceType { get; init; } = string.Empty;

    public string SourceId { get; init; } = string.Empty;
}

public interface IBillingChargeFinanceTrigger
{
    Task NotifyChargedAsync(
        BillingChargeFinanceContext context,
        CancellationToken cancellationToken = default);
}

public sealed class BillingChargeFinanceContext
{
    public Guid ChargeId { get; init; }

    public bool IsNew { get; init; }

    public Guid PartnerId { get; init; }

    public Guid? TenantId { get; init; }

    public BillingChargeTarget ChargeTarget { get; init; }

    public BillingChargeKind Kind { get; init; }

    public decimal Amount { get; init; }

    public string Currency { get; init; } = CommissionConsts.DefaultCurrency;

    public string IdempotencyKey { get; init; } = string.Empty;

    public Guid? CommissionLedgerEntryId { get; init; }
}
