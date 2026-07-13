using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Zahy.Settlement;

/// <summary>
/// Default in-memory reflected-order source — MOCK-ONLY posture, matching this gate's compute-only
/// scope (mock statement data, flags OFF). The production adapter that joins ReflectedPartnerOrder /
/// OrderRecord / SettlementCostMarkupSnapshot over the Connectors external-id mapping arrives with
/// the live wiring gate; registration is TryAdd so composition can replace it without ceremony.
/// </summary>
public sealed class InMemoryAggregatorReflectedOrderSource : IAggregatorReflectedOrderSource
{
    private readonly ConcurrentDictionary<Guid, List<ReflectedOrderMatchView>> _byPartner = new();

    public void Seed(Guid partnerId, IEnumerable<ReflectedOrderMatchView> views)
    {
        _byPartner[partnerId] = views.ToList();
    }

    public Task<IReadOnlyList<ReflectedOrderMatchView>> GetForPartnerPeriodAsync(
        Guid partnerId,
        DateTime periodFrom,
        DateTime periodTo,
        CancellationToken cancellationToken = default)
    {
        var views = _byPartner.TryGetValue(partnerId, out var list)
            ? list.Where(v => v.OrderDate >= periodFrom && v.OrderDate <= periodTo).ToList()
            : new List<ReflectedOrderMatchView>();

        return Task.FromResult<IReadOnlyList<ReflectedOrderMatchView>>(views);
    }
}
