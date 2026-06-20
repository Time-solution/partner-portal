using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace Zahy.Settlement;

public class SettlementEventStore : ISettlementEventStore, ITransientDependency
{
    private readonly IRepository<SettlementWebhookEvent, Guid> _repository;

    public SettlementEventStore(IRepository<SettlementWebhookEvent, Guid> repository)
    {
        _repository = repository;
    }

    public Task InsertAsync(SettlementWebhookEvent webhookEvent, CancellationToken cancellationToken = default) =>
        _repository.InsertAsync(webhookEvent, autoSave: true, cancellationToken: cancellationToken);

    public async Task<IReadOnlyList<SettlementWebhookEvent>> GetByCaseAsync(Guid settlementCaseId, CancellationToken cancellationToken = default)
    {
        var queryable = await _repository.GetQueryableAsync();
        return queryable
            .Where(x => x.SettlementCaseId == settlementCaseId)
            .OrderBy(x => x.ReceivedAt)
            .ToList();
    }
}
