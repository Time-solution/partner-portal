using System.Threading;
using System.Threading.Tasks;

namespace Zahy.Commission;

public class NullCommissionLedgerFinanceTrigger : ICommissionLedgerFinanceTrigger
{
    public Task NotifyAccruedAsync(
        CommissionLedgerFinanceAccrualContext context,
        CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}

public class NullBillingChargeFinanceTrigger : IBillingChargeFinanceTrigger
{
    public Task NotifyChargedAsync(
        BillingChargeFinanceContext context,
        CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
