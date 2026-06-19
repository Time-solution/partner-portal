using System.Threading;
using System.Threading.Tasks;

namespace Zahy.OrderLedger;

/// <summary>No-op notifier for tests or hosts without webhook wiring.</summary>
public class NullOrderLedgerWebhookNotifier : IOrderLedgerWebhookNotifier
{
    public Task NotifyIngestedAsync(
        OrderRecord record,
        OrderStatus? previousStatus,
        CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
