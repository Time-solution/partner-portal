using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;
using Zahy.Settlement;

namespace Zahy.PartnerCatalog;

/// <summary>
/// A sale from the partner warehouse routes to the correct EXISTING settlement mode, but settlement
/// dispatch is FLAGGED OFF — no money is moved. The fake bridge throws if ever called, proving the
/// flagged-off path never reaches money.
/// </summary>
public class ConsignmentSaleSettlementRouterTests
{
    private static readonly Guid PartnerId = Guid.Parse("44444444-4444-4444-4444-444444444004");
    private static readonly Guid TenantId = Guid.Parse("11111111-1111-1111-1111-111111111001");
    private static readonly DateTime At = new(2026, 6, 20, 12, 0, 0, DateTimeKind.Utc);

    private sealed class ThrowingParticipationBridge : IPartnerCatalogParticipationBridge
    {
        public Task<PartnerCatalogParticipationResult> DispatchAsync(
            SettlementCostMarkupSnapshot snapshot,
            PartnerCatalogItem catalogItem,
            MerchantActivation activation,
            string? billingPeriodKey = null,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Settlement must not be dispatched while flagged off.");

        public Task<PartnerCatalogParticipationResult> ReversePrincipalAsync(
            SettlementCostMarkupSnapshot originalSnapshot,
            PartnerCatalogItem catalogItem,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Reversal must not run while flagged off.");
    }

    [Theory]
    [InlineData(ConsignmentOwnershipMode.MerchantOwned, SettlementParticipationMode.ReflectionOnly)]
    [InlineData(ConsignmentOwnershipMode.PartnerBought, SettlementParticipationMode.Principal)]
    public async Task Sale_Routes_To_Correct_Mode_But_Flagged_Off_Moves_No_Money(
        ConsignmentOwnershipMode ownership,
        SettlementParticipationMode expectedMode)
    {
        var router = new ConsignmentSaleSettlementRouter(
            new ThrowingParticipationBridge(),
            Options.Create(new ConsignmentSettlementOptions())); // default: dispatch disabled

        var (item, activation, snapshot) = CreateConsignmentSale(ownership);

        var result = await router.RouteSaleAsync(snapshot, item, activation);

        result.Outcome.ShouldBe(ConsignmentSaleRoutingOutcome.FlaggedOff);
        result.ResolvedOwnershipMode.ShouldBe(ownership);
        result.ResolvedParticipationMode.ShouldBe(expectedMode);
        result.ParticipationResult.ShouldBeNull();
        snapshot.SettlementCaseId.ShouldBeNull();
        snapshot.BillingChargeId.ShouldBeNull();
    }

    [Fact]
    public async Task NonConsignment_Offering_Is_Skipped()
    {
        var router = new ConsignmentSaleSettlementRouter(
            new ThrowingParticipationBridge(),
            Options.Create(new ConsignmentSettlementOptions()));

        var item = PartnerCatalogItem.Create(
            Guid.NewGuid(),
            PartnerId,
            "DLV-1",
            "Delivery",
            null,
            PartnerCatalogOfferingKind.DeliveryFulfilmentPerOrder,
            Money.Of(10m, vatInclusive: true));
        item.Publish(At);
        var activation = MerchantActivation.Create(Guid.NewGuid(), TenantId, item, Money.Of(15m, vatInclusive: true));
        activation.Activate(At);
        var snapshot = SettlementCostMarkupSnapshot.Create(
            Guid.NewGuid(),
            activation,
            item,
            Money.Of(15m, vatInclusive: true),
            SettlementCostMarkupTrigger.Order,
            "order:dlv:v1");

        var result = await router.RouteSaleAsync(snapshot, item, activation);

        result.Outcome.ShouldBe(ConsignmentSaleRoutingOutcome.Skipped);
    }

    private static (PartnerCatalogItem Item, MerchantActivation Activation, SettlementCostMarkupSnapshot Snapshot)
        CreateConsignmentSale(ConsignmentOwnershipMode ownership)
    {
        var item = PartnerCatalogItem.Create(
            Guid.NewGuid(),
            PartnerId,
            "JUMP-SKU-1",
            "Consignment SKU",
            null,
            PartnerCatalogOfferingKind.ConsignmentFulfilment,
            Money.Of(25m, vatInclusive: true),
            consignmentOwnershipMode: ownership);
        item.Publish(At);

        var activation = MerchantActivation.Create(
            Guid.NewGuid(),
            TenantId,
            item,
            Money.Of(40m, vatInclusive: true));
        activation.Activate(At);

        var snapshot = SettlementCostMarkupSnapshot.Create(
            Guid.NewGuid(),
            activation,
            item,
            Money.Of(40m, vatInclusive: true),
            SettlementCostMarkupTrigger.OrderLine,
            "order:jump-ord-1:v1",
            orderLineId: "JUMP-ORD-1-L1");

        return (item, activation, snapshot);
    }
}
