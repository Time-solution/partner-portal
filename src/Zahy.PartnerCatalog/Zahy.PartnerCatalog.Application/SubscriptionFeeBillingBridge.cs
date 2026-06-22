using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Zahy.Commission;
using Zahy.Settlement;

namespace Zahy.PartnerCatalog;

public sealed class SubscriptionFeeBillingBridge : ITransientDependency
{
    private const decimal DefaultVatRate = SettlementVatOptions.DefaultStandardRate;

    private readonly IBillingChargeService _billingChargeService;
    private readonly ISettlementFlowProfileResolver _profileResolver;

    public SubscriptionFeeBillingBridge(
        IBillingChargeService billingChargeService,
        ISettlementFlowProfileResolver profileResolver)
    {
        _billingChargeService = billingChargeService;
        _profileResolver = profileResolver;
    }

    public async Task<(PartnerCatalogParticipationOutcome Outcome, Guid? BillingChargeId, decimal OutputVat, decimal TotalDebits, decimal TotalCredits)>
        ChargeAsync(
            SettlementCostMarkupSnapshot snapshot,
            PartnerCatalogItem catalogItem,
            MerchantActivation activation,
            string billingPeriodKey,
            CancellationToken cancellationToken = default)
    {
        Check.NotNull(snapshot, nameof(snapshot));
        Check.NotNull(catalogItem, nameof(catalogItem));
        Check.NotNull(activation, nameof(activation));

        if (!SubscriptionFeeDispatchPolicy.ShouldBill(catalogItem, snapshot))
        {
            return (PartnerCatalogParticipationOutcome.Skipped, null, 0m, 0m, 0m);
        }

        var periodKey = PartnerCatalogBillingIdempotency.NormalizeBillingPeriodKey(billingPeriodKey);
        var idempotencyKey = PartnerCatalogBillingIdempotency.BuildMerchantSubscriptionPeriodKey(
            snapshot.TenantId,
            activation.Id,
            periodKey);

        var fee = activation.ResalePrice;
        var chargeResult = await _billingChargeService.ChargeAsync(
            new BillingChargeRequest
            {
                PartnerId = snapshot.PartnerId,
                ChargeTarget = BillingChargeTarget.Merchant,
                TenantId = snapshot.TenantId,
                Kind = BillingChargeKind.Subscription,
                Amount = fee.Amount,
                Currency = fee.Currency,
                IdempotencyKey = idempotencyKey,
                PeriodKey = periodKey,
                Description = $"Partner catalog subscription {periodKey}"
            },
            cancellationToken);

        if (chargeResult.IsNew)
        {
            snapshot.LinkBillingCharge(chargeResult.ChargeId);
        }
        else if (!snapshot.BillingChargeId.HasValue)
        {
            snapshot.LinkBillingCharge(chargeResult.ChargeId);
        }

        var journal = BuildBalancedJournal(fee, snapshot.CreatedAt, snapshot.Id);
        var outputVat = journal.Lines
            .Where(l => l.Account == SettlementAccountType.VatOutput && l.Direction == EntryDirection.Credit)
            .Sum(l => l.Amount.Amount);

        return (
            chargeResult.IsNew
                ? PartnerCatalogParticipationOutcome.SubscriptionCharged
                : PartnerCatalogParticipationOutcome.SubscriptionDuplicate,
            chargeResult.ChargeId,
            outputVat,
            journal.TotalDebits.Amount,
            journal.TotalCredits.Amount);
    }

    internal static Journal BuildBalancedJournal(Money feeInclusive, DateTime postedAt, Guid snapshotId)
    {
        var journal = SubscriptionFeeJournalBuilder.Build(
            feeInclusive,
            DefaultVatRate,
            postedAt,
            Guid.NewGuid(),
            $"pcat:sub:snapshot:{snapshotId:D}");

        var profile = new ServiceFlowProfile();
        BookIsolationGuard.EnsureWithinBook(profile, journal);
        return journal;
    }
}
