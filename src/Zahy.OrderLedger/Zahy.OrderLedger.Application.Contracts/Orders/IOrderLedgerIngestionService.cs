using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Zahy.OrderLedger;

/// <summary>
/// Pulls order snapshots from an external source (CDC, webhook, batch file).
/// Phase 3: <see cref="FakeOrderSource"/> only. Later: Commerce CDC adapter.
/// </summary>
public interface IOrderSource
{
    Task<IReadOnlyList<OrderSourceSnapshot>> PollAsync(
        OrderSourcePollRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class OrderIngestResult
{
    public Guid OrderRecordId { get; init; }

    public bool IsNew { get; init; }

    public string SourceSystem { get; init; } = string.Empty;

    public string SourceOrderId { get; init; } = string.Empty;

    public long SourceVersion { get; init; }
}

public sealed class OrderIngestBatchResult
{
    public int PolledCount { get; init; }

    public int IngestedCount { get; init; }

    public int SkippedDuplicateCount { get; init; }

    public IReadOnlyList<OrderIngestResult> Results { get; init; } = [];
}

public interface IOrderLedgerIngestionService
{
    Task<OrderIngestResult> IngestSnapshotAsync(
        OrderSourceSnapshot snapshot,
        CancellationToken cancellationToken = default);

    Task<OrderIngestBatchResult> PollAndIngestAsync(
        OrderSourcePollRequest request,
        CancellationToken cancellationToken = default);
}
