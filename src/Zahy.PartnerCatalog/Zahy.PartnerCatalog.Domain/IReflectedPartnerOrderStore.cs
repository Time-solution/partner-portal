using System.Threading;
using System.Threading.Tasks;

namespace Zahy.PartnerCatalog;

public interface IReflectedPartnerOrderStore
{
    Task<ReflectedPartnerOrder?> FindByKeyAsync(
        string externalTransactionId,
        string orderLineId,
        CancellationToken cancellationToken = default);

    Task InsertAsync(ReflectedPartnerOrder order, CancellationToken cancellationToken = default);
}
