using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;
using Zahy.Commission;

namespace Zahy.Finance;

public class CommissionLedgerFinanceTrigger : ApplicationService, ICommissionLedgerFinanceTrigger
{
    private readonly FinancePostingIngestionService _postingIngestionService;

    public CommissionLedgerFinanceTrigger(FinancePostingIngestionService postingIngestionService)
    {
        _postingIngestionService = postingIngestionService;
    }

    public Task NotifyAccruedAsync(
        CommissionLedgerFinanceAccrualContext context,
        CancellationToken cancellationToken = default) =>
        _postingIngestionService.IngestCommissionAccrualAsync(context, cancellationToken);
}
