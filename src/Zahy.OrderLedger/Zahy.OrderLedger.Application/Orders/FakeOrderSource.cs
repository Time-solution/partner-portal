using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;

namespace Zahy.OrderLedger;

/// <summary>In-memory order source for tests and local dev — not Commerce CDC.</summary>
public class FakeOrderSource : IOrderSource, ISingletonDependency
{
    private readonly ConcurrentDictionary<string, OrderSourceSnapshot> _snapshots = new();

    public void Seed(OrderSourceSnapshot snapshot)
    {
        var key = BuildKey(snapshot.SourceSystem, snapshot.SourceOrderId, snapshot.SourceVersion);
        _snapshots[key] = snapshot;
    }

    public void Reset() => _snapshots.Clear();

    public Task<IReadOnlyList<OrderSourceSnapshot>> PollAsync(
        OrderSourcePollRequest request,
        CancellationToken cancellationToken = default)
    {
        var query = _snapshots.Values
            .Where(x => string.Equals(x.SourceSystem, request.SourceSystem, StringComparison.OrdinalIgnoreCase));

        if (request.Since.HasValue)
        {
            query = query.Where(x => x.SourceTimestamp >= request.Since.Value);
        }

        var results = query
            .OrderBy(x => x.SourceTimestamp)
            .ThenBy(x => x.SourceOrderId)
            .ThenBy(x => x.SourceVersion)
            .Take(request.MaxResults)
            .ToList();

        return Task.FromResult<IReadOnlyList<OrderSourceSnapshot>>(results);
    }

    private static string BuildKey(string sourceSystem, string sourceOrderId, long sourceVersion) =>
        $"{sourceSystem}:{sourceOrderId}:{sourceVersion}";
}
