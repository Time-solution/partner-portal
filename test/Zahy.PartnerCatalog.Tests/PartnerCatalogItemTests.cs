using System;
using Shouldly;
using Volo.Abp;
using Xunit;
using Zahy.Settlement;

namespace Zahy.PartnerCatalog;

public class PartnerCatalogItemTests
{
    private static readonly Guid PartnerId = Guid.Parse("22222222-2222-2222-2222-222222222001");

    [Fact]
    public void Create_ServiceOneOff_Uses_Principal_And_Default_Trigger()
    {
        var item = PartnerCatalogItem.Create(
            Guid.NewGuid(),
            PartnerId,
            "SVC-SETUP",
            "Premium delivery integration setup",
            "One-time onboarding",
            PartnerCatalogOfferingKind.ServiceOneOff,
            Money.Of(70m, vatInclusive: true));

        item.Status.ShouldBe(PartnerCatalogItemStatus.Draft);
        item.DefaultVatTreatment.ShouldBe(VatTreatment.Principal);
        item.SettlementTriggerMode.ShouldBe(SettlementTriggerMode.OnActivation);
        item.EffectiveSettlementBook.ShouldBe(SettlementBook.Integration);
        item.PartnerCost.Amount.ShouldBe(70m);
        item.PartnerCost.VatInclusive.ShouldBeTrue();
    }

    [Fact]
    public void Create_DeliveryFulfilmentPerOrder_Sets_Pattern_A_Fields()
    {
        var item = PartnerCatalogItem.Create(
            Guid.NewGuid(),
            PartnerId,
            "DLV-STD",
            "Standard delivery",
            null,
            PartnerCatalogOfferingKind.DeliveryFulfilmentPerOrder,
            Money.Of(10m, vatInclusive: true),
            carrierServiceCode: "OTO-STD");

        item.SettlementTriggerMode.ShouldBe(SettlementTriggerMode.PerOrder);
        item.EffectiveSettlementBook.ShouldBe(SettlementBook.Marketplace);
        item.FulfilmentUnit.ShouldBe(PartnerCatalogFulfilmentUnit.PerShipment);
        item.CarrierServiceCode.ShouldBe("OTO-STD");
        item.RequiresPlatformCatalogSync.ShouldBeFalse();
    }

    [Fact]
    public void Create_FnBItemsPerSale_Sets_Pattern_B_Fields()
    {
        var item = PartnerCatalogItem.Create(
            Guid.NewGuid(),
            PartnerId,
            "MENU-BURGER",
            "Classic burger",
            null,
            PartnerCatalogOfferingKind.FnBItemsPerSale,
            Money.Of(25m, vatInclusive: true),
            externalMenuItemId: "JAHEZ-88421",
            menuCategoryCode: "BURGERS");

        item.SettlementTriggerMode.ShouldBe(SettlementTriggerMode.PerOrderLine);
        item.RequiresPlatformCatalogSync.ShouldBeTrue();
        item.ExternalMenuItemId.ShouldBe("JAHEZ-88421");
        item.MenuCategoryCode.ShouldBe("BURGERS");
    }

    [Fact]
    public void Create_Rejects_Non_Vat_Inclusive_Cost()
    {
        Should.Throw<BusinessException>(() =>
                PartnerCatalogItem.Create(
                    Guid.NewGuid(),
                    PartnerId,
                    "X",
                    "Name",
                    null,
                    PartnerCatalogOfferingKind.ServiceOneOff,
                    Money.Of(70m, vatInclusive: false)))
            .Code.ShouldBe(PartnerCatalogErrorCodes.InvalidPartnerCost);
    }

    [Fact]
    public void Publish_And_Archive_Follow_State_Machine()
    {
        var at = new DateTime(2026, 6, 1, 9, 0, 0, DateTimeKind.Utc);
        var item = PartnerCatalogItem.Create(
            Guid.NewGuid(),
            PartnerId,
            "SVC-1",
            "Service",
            null,
            PartnerCatalogOfferingKind.ServiceOneOff,
            Money.Of(70m, vatInclusive: true));

        item.Publish(at);
        item.Status.ShouldBe(PartnerCatalogItemStatus.Active);

        item.Archive(at.AddDays(1));
        item.Status.ShouldBe(PartnerCatalogItemStatus.Archived);
        item.ArchivedAt.ShouldBe(at.AddDays(1));
    }

    [Fact]
    public void Active_To_Draft_Is_Rejected()
    {
        var item = PartnerCatalogItem.Create(
            Guid.NewGuid(),
            PartnerId,
            "SVC-1",
            "Service",
            null,
            PartnerCatalogOfferingKind.ServiceOneOff,
            Money.Of(70m, vatInclusive: true));

        item.Publish(DateTime.UtcNow);

        Should.Throw<BusinessException>(() => item.Discard(DateTime.UtcNow))
            .Code.ShouldBe(PartnerCatalogErrorCodes.InvalidStatusTransition);
    }
}
