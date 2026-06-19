using System.Threading;
using System.Threading.Tasks;

namespace Zahy.OrderLedger;

public class NullOrderLedgerCommissionTrigger : IOrderLedgerCommissionTrigger
{
    public Task NotifyIngestedAsync(
        OrderRecord record,
        OrderStatus? previousStatus,
        CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
