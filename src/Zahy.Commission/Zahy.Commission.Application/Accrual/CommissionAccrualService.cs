using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;
using Zahy.OrderLedger;
using Zahy.PartnerPlatform.Partners;

namespace Zahy.Commission;

public class CommissionAccrualService : ApplicationService, ICommissionAccrualService
{
    private readonly IRepository<CommissionRule, Guid> _ruleRepository;
    private readonly ICommissionRuleWinnerResolver _ruleWinnerResolver;
    private readonly ICommissionBasisAmountResolver _basisAmountResolver;
    private readonly ICommissionCalculator _calculator;
    private readonly ICommissionLedgerService _ledgerService;
    private readonly ICommissionPartnerTypeLookup _partnerTypeLookup;
    private readonly IBillingChargeService _billingChargeService;

    public CommissionAccrualService(
        IRepository<CommissionRule, Guid> ruleRepository,
        ICommissionRuleWinnerResolver ruleWinnerResolver,
        ICommissionBasisAmountResolver basisAmountResolver,
        ICommissionCalculator calculator,
        ICommissionLedgerService ledgerService,
        ICommissionPartnerTypeLookup partnerTypeLookup,
        IBillingChargeService billingChargeService)
    {
        _ruleRepository = ruleRepository;
        _ruleWinnerResolver = ruleWinnerResolver;
        _basisAmountResolver = basisAmountResolver;
        _calculator = calculator;
        _ledgerService = ledgerService;
        _partnerTypeLookup = partnerTypeLookup;
        _billingChargeService = billingChargeService;
    }

    [UnitOfWork]
    public virtual async Task AccrueForIngestedOrderAsync(
        OrderRecord record,
        CancellationToken cancellationToken = default)
    {
        if (record.PartnerId == null || record.PaymentStatus != PaymentStatus.Paid)
        {
            return;
        }

        if (!record.HasReliableCommissionSubtotal())
        {
            Logger.LogWarning(
                "Skipping commission accrual for {SourceSystem}:{SourceOrderId}:v{SourceVersion} — " +
                "no explicit subtotal or line items (TotalAmount={TotalAmount}, Tax={TaxAmount}, Delivery={DeliveryFee}). " +
                "Commission basis must not include tax or delivery.",
                record.SourceSystem,
                record.SourceOrderId,
                record.SourceVersion,
                record.TotalAmount,
                record.TaxAmount,
                record.DeliveryFee);
            return;
        }

        var partnerType = await _partnerTypeLookup.GetPartnerTypeAsync(record.PartnerId.Value, cancellationToken);
        var activeRules = await FindActiveSaleRulesAsync(record.SourceTimestamp, cancellationToken);
        var matchingRules = activeRules
            .Where(rule => IsRuleMatch(rule, record, partnerType))
            .ToList();

        if (matchingRules.Count == 0)
        {
            return;
        }

        var winners = _ruleWinnerResolver.ResolveWinningRules(matchingRules.Select(x => x.ToSnapshot()));
        var orderAmounts = ToOrderAmounts(record);
        var activationCharged = false;

        foreach (var winner in winners)
        {
            var rule = matchingRules.Single(x => x.Id == winner.Id);
            var basisAmount = _basisAmountResolver.Resolve(orderAmounts, winner);
            var calculation = _calculator.Compute(basisAmount, winner.BasisDefinition, record.Currency);

            var accrual = await _ledgerService.AccrueAsync(new CommissionAccrualRequest
            {
                PartnerId = record.PartnerId.Value,
                TenantId = record.TenantId,
                RuleId = rule.Id,
                SourceType = CommissionSourceTypes.OrderPaid,
                SourceId = BuildSourceId(record),
                OrderRecordId = record.Id,
                BasisAmount = basisAmount,
                ComputedCommission = calculation.ComputedCommission,
                Direction = rule.Direction,
                Currency = record.Currency
            }, cancellationToken);

            if (accrual.IsNew)
            {
                await _billingChargeService.ChargeCommissionAccrualAsync(
                    accrual,
                    record.PartnerId.Value,
                    record.TenantId,
                    record.Currency,
                    cancellationToken);

                if (!activationCharged)
                {
                    await _billingChargeService.ChargeActivationIfConfiguredAsync(
                        record.PartnerId.Value,
                        record.TenantId,
                        cancellationToken);
                    activationCharged = true;
                }
            }
        }
    }

    private async Task<List<CommissionRule>> FindActiveSaleRulesAsync(
        DateTime effectiveAt,
        CancellationToken cancellationToken)
    {
        var queryable = await _ruleRepository.GetQueryableAsync();
        return queryable
            .Where(rule =>
                rule.IsEnabled &&
                rule.TriggerType == CommissionTriggerType.Sale &&
                rule.EffectiveFromUtc <= effectiveAt &&
                (rule.EffectiveToUtc == null || rule.EffectiveToUtc > effectiveAt))
            .ToList();
    }

    private static bool IsRuleMatch(CommissionRule rule, OrderRecord record, PartnerType? partnerType)
    {
        if (record.PartnerId == null)
        {
            return false;
        }

        return rule.ScopeKind switch
        {
            CommissionScopeKind.Partner => rule.ScopePartnerId == record.PartnerId,
            CommissionScopeKind.PartnerType => partnerType != null && rule.ScopePartnerType == partnerType,
            CommissionScopeKind.Category => false,
            CommissionScopeKind.Product => false,
            _ => false
        };
    }

    private static CommissionOrderAmounts ToOrderAmounts(OrderRecord record) =>
        new()
        {
            Subtotal = record.Subtotal,
            TaxAmount = record.TaxAmount,
            DeliveryFee = record.DeliveryFee,
            TotalAmount = record.TotalAmount,
            Currency = record.Currency
        };

    public static string BuildSourceId(OrderRecord record) =>
        $"{record.SourceSystem}:{record.SourceOrderId}:{record.SourceVersion}";
}
