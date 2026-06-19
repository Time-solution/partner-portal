using System.Threading;
using System.Threading.Tasks;
using Zahy.PartnerPlatform.Partners;

namespace Zahy.Commission;

public interface ICommissionAccrualService
{
    Task AccrueForIngestedOrderAsync(
        OrderLedger.OrderRecord record,
        CancellationToken cancellationToken = default);
}

public interface ICommissionPartnerTypeLookup
{
    Task<PartnerType?> GetPartnerTypeAsync(
        Guid partnerId,
        CancellationToken cancellationToken = default);
}
