using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace Zahy.Settlement;

public class SettlementCaseStore : ISettlementCaseStore, ITransientDependency
{
    private readonly IRepository<SettlementCase, Guid> _repository;

    public SettlementCaseStore(IRepository<SettlementCase, Guid> repository)
    {
        _repository = repository;
    }

    public async Task<SettlementCase?> FindByKeyAsync(SettlementBook book, string externalTransactionId, CancellationToken cancellationToken = default)
    {
        var queryable = await _repository.GetQueryableAsync();
        return queryable.FirstOrDefault(x => x.Book == book && x.ExternalTransactionId == externalTransactionId);
    }

    public Task InsertAsync(SettlementCase settlementCase, CancellationToken cancellationToken = default) =>
        _repository.InsertAsync(settlementCase, autoSave: true, cancellationToken: cancellationToken);

    public Task UpdateAsync(SettlementCase settlementCase, CancellationToken cancellationToken = default) =>
        _repository.UpdateAsync(settlementCase, autoSave: true, cancellationToken: cancellationToken);
}
