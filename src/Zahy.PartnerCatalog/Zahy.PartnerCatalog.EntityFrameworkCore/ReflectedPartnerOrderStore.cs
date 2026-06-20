using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace Zahy.PartnerCatalog;

public sealed class ReflectedPartnerOrderStore : IReflectedPartnerOrderStore, ITransientDependency
{
    private readonly IRepository<ReflectedPartnerOrder, Guid> _repository;

    public ReflectedPartnerOrderStore(IRepository<ReflectedPartnerOrder, Guid> repository)
    {
        _repository = repository;
    }

    public async Task<ReflectedPartnerOrder?> FindByKeyAsync(
        string externalTransactionId,
        string orderLineId,
        CancellationToken cancellationToken = default)
    {
        var queryable = await _repository.GetQueryableAsync();
        return queryable.FirstOrDefault(x =>
            x.ExternalTransactionId == externalTransactionId && x.OrderLineId == orderLineId);
    }

    public Task InsertAsync(ReflectedPartnerOrder order, CancellationToken cancellationToken = default) =>
        _repository.InsertAsync(order, autoSave: true, cancellationToken: cancellationToken);
}
