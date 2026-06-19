using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;
using Zahy.OrderLedger;

namespace Zahy.Commission;

public class OrderLedgerCommissionTrigger : ApplicationService, IOrderLedgerCommissionTrigger
{
    private readonly ICommissionAccrualService _accrualService;

    public OrderLedgerCommissionTrigger(ICommissionAccrualService accrualService)
    {
        _accrualService = accrualService;
    }

    public Task NotifyIngestedAsync(
        OrderRecord record,
        OrderStatus? previousStatus,
        CancellationToken cancellationToken = default) =>
        _accrualService.AccrueForIngestedOrderAsync(record, cancellationToken);
}
