using System;
using Shouldly;
using Volo.Abp;
using Xunit;
using Zahy.Settlement;

namespace Zahy.PartnerCatalog;

public class SettlementCostMarkupSnapshotTests
{
    private static readonly Guid PartnerId = Guid.Parse("22222222-2222-2222-2222-222222222001");
    private static readonly Guid TenantId = Guid.Parse("11111111-1111-1111-1111-111111111001");
    private static readonly DateTime T = new(2026, 6, 2, 14, 30, 0, DateTimeKind.Utc);

    [Fact]
    public void Create_From_Activation_Uses_Buy_From_Item_And_Principal_Vat()
    {
        var item = CreatePublishedServiceItem();
        var activation = CreateActiveActivation(item, Money.Of(100m, vatInclusive: true));

        var snapshot = SettlementCostMarkupSnapshot.Create(
            Guid.NewGuid(),
            activation,
            item,
            Money.Of(100m, vatInclusive: true),
            SettlementCostMarkupTrigger.Activation,
            "pcat:activation:test:v1");

        snapshot.BuyPrice.Amount.ShouldBe(70m);
        snapshot.BuyPrice.VatInclusive.ShouldBeTrue();
        snapshot.SellPrice.Amount.ShouldBe(100m);
        snapshot.VatTreatment.ShouldBe(VatTreatment.Principal);
        snapshot.SettlementBook.ShouldBe(SettlementBook.Integration);
        snapshot.Trigger.ShouldBe(SettlementCostMarkupTrigger.Activation);
        snapshot.SettlementCaseId.ShouldBeNull();
        snapshot.SellPriceSource.ShouldBe(PartnerCatalogSellPriceSource.Unresolved);
        snapshot.OrderLineId.ShouldBe(string.Empty);
    }

    [Fact]
    public void Create_PerOrderLine_Stores_Normalized_OrderLineId()
    {
        var item = CreatePublishedDeliveryItem();
        var activation = CreateActiveActivation(item, Money.Of(15m, vatInclusive: true));

        var snapshot = SettlementCostMarkupSnapshot.Create(
            Guid.NewGuid(),
            activation,
            item,
            Money.Of(40m, vatInclusive: true),
            SettlementCostMarkupTrigger.OrderLine,
            "pcat:order:ORD-8002:line-1:v1",
            orderLineId: " ORD-8002-L1 ");

        snapshot.OrderLineId.ShouldBe("ord-8002-l1");
        snapshot.ExternalTransactionId.ShouldBe("pcat:order:ord-8002:line-1:v1");
        snapshot.SettlementCaseId.ShouldBeNull();
    }

    [Fact]
    public void Create_Rejects_Non_Vat_Inclusive_Sell_Price()
    {
        var item = CreatePublishedServiceItem();
        var activation = CreateActiveActivation(item, Money.Of(100m, vatInclusive: true));

        Should.Throw<BusinessException>(() =>
                SettlementCostMarkupSnapshot.Create(
                    Guid.NewGuid(),
                    activation,
                    item,
                    Money.Of(100m, vatInclusive: false),
                    SettlementCostMarkupTrigger.Activation,
                    "pcat:activation:test:v1"))
            .Code.ShouldBe(PartnerCatalogErrorCodes.InvalidResalePrice);
    }

    [Fact]
    public void Create_OrderLine_Trigger_Requires_OrderLineId()
    {
        var item = CreatePublishedFnBItem();
        var activation = CreateActiveActivation(item, Money.Of(35m, vatInclusive: true));

        Should.Throw<BusinessException>(() =>
                SettlementCostMarkupSnapshot.Create(
                    Guid.NewGuid(),
                    activation,
                    item,
                    Money.Of(40m, vatInclusive: true),
                    SettlementCostMarkupTrigger.OrderLine,
                    "pcat:order:line:v1",
                    orderLineId: null))
            .Code.ShouldBe(PartnerCatalogErrorCodes.MissingOrderLineId);
    }

    [Fact]
    public void Same_ExternalTxn_Different_OrderLineIds_Do_Not_Share_Storage_Key()
    {
        PartnerCatalogSnapshotIdempotency.BuildStorageKey("order:ord-1:v1", "L1")
            .ShouldNotBe(PartnerCatalogSnapshotIdempotency.BuildStorageKey("order:ord-1:v1", "L2"));
    }

    [Fact]
    public void Same_ExternalTxn_Both_Null_OrderLineIds_Share_Storage_Key()
    {
        PartnerCatalogSnapshotIdempotency.BuildStorageKey("order:ord-1:v1", null)
            .ShouldBe(PartnerCatalogSnapshotIdempotency.BuildStorageKey("order:ord-1:v1", string.Empty));
    }

    private static PartnerCatalogItem CreatePublishedServiceItem()
    {
        var item = PartnerCatalogItem.Create(
            Guid.NewGuid(),
            PartnerId,
            "SVC-1",
            "Service",
            null,
            PartnerCatalogOfferingKind.ServiceOneOff,
            Money.Of(70m, vatInclusive: true));
        item.Publish(T);
        return item;
    }

    private static PartnerCatalogItem CreatePublishedDeliveryItem()
    {
        var item = PartnerCatalogItem.Create(
            Guid.NewGuid(),
            PartnerId,
            "DLV-1",
            "Delivery",
            null,
            PartnerCatalogOfferingKind.DeliveryFulfilmentPerOrder,
            Money.Of(10m, vatInclusive: true));
        item.Publish(T);
        return item;
    }

    private static PartnerCatalogItem CreatePublishedFnBItem()
    {
        var item = PartnerCatalogItem.Create(
            Guid.NewGuid(),
            PartnerId,
            "MENU-1",
            "Burger",
            null,
            PartnerCatalogOfferingKind.FnBItemsPerSale,
            Money.Of(25m, vatInclusive: true),
            externalMenuItemId: "JAHEZ-1");
        item.Publish(T);
        return item;
    }

    private static MerchantActivation CreateActiveActivation(PartnerCatalogItem item, Money resalePrice)
    {
        var activation = MerchantActivation.Create(
            Guid.NewGuid(),
            TenantId,
            item,
            resalePrice);
        activation.Activate(T);
        return activation;
    }
}
