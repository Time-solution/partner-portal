using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;
using Zahy.Commission;

namespace Zahy.Finance;

public class BillingChargeFinanceTrigger : ApplicationService, IBillingChargeFinanceTrigger
{
    private readonly FinancePostingIngestionService _postingIngestionService;

    public BillingChargeFinanceTrigger(FinancePostingIngestionService postingIngestionService)
    {
        _postingIngestionService = postingIngestionService;
    }

    public Task NotifyChargedAsync(
        BillingChargeFinanceContext context,
        CancellationToken cancellationToken = default) =>
        _postingIngestionService.IngestBillingChargeAsync(context, cancellationToken);
}
