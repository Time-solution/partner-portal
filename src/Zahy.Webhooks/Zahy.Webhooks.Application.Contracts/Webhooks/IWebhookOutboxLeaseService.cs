using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Zahy.Webhooks;

/// <summary>
/// Atomically claims due outbox rows for exclusive processing (multi-instance safe).
/// </summary>
public interface IWebhookOutboxLeaseService
{
    Task<IReadOnlyList<Guid>> ClaimDueBatchAsync(
        int batchSize,
        DateTime now,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken = default);
}
