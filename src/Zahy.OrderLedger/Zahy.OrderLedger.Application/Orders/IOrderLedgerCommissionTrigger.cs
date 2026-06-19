using System.Threading;
using System.Threading.Tasks;

namespace Zahy.OrderLedger;

/// <summary>
/// In-process commission trigger parallel to <see cref="IOrderLedgerWebhookNotifier"/>.
/// Invoked only on new ledger inserts within the same unit of work — not via webhook outbox.
/// </summary>
public interface IOrderLedgerCommissionTrigger
{
    Task NotifyIngestedAsync(
        OrderRecord record,
        OrderStatus? previousStatus,
        CancellationToken cancellationToken = default);
}
