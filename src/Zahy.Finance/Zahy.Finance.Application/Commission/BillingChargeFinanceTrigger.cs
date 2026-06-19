using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;
using Zahy.Commission;

namespace Zahy.Finance;

public class BillingChargeFinanceTrigger : ApplicationService, IBillingChargeFinanceTrigger
{
    private readonly FinancePostingIngestionService _postingIngestionService;
    private readonly IInvoiceTrigger _invoiceTrigger;

    public BillingChargeFinanceTrigger(
        FinancePostingIngestionService postingIngestionService,
        IInvoiceTrigger invoiceTrigger)
    {
        _postingIngestionService = postingIngestionService;
        _invoiceTrigger = invoiceTrigger;
    }

    public async Task NotifyChargedAsync(
        BillingChargeFinanceContext context,
        CancellationToken cancellationToken = default)
    {
        await _postingIngestionService.IngestBillingChargeAsync(context, cancellationToken);
        await _invoiceTrigger.TryGenerateForBillingChargeAsync(
            new FinanceBillingInvoiceTriggerContext
            {
                BillingChargeId = context.ChargeId,
                IsNew = context.IsNew,
                PartnerId = context.PartnerId,
                TenantId = context.TenantId,
                AccountKind = context.ChargeTarget == BillingChargeTarget.Merchant
                    ? FinanceAccountKind.Merchant
                    : FinanceAccountKind.Partner
            },
            cancellationToken);
    }
}
