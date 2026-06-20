using System;
using Shouldly;
using Volo.Abp;
using Xunit;
using Zahy.Settlement;

namespace Zahy.PartnerCatalog;

public class PlatformCatalogLinkTests
{
    private static readonly Guid PartnerId = Guid.Parse("33333333-3333-3333-3333-333333333003");

    [Fact]
    public void CreateDeferredShape2_For_FnB_Item()
    {
        var item = PartnerCatalogItem.Create(
            Guid.NewGuid(),
            PartnerId,
            "MENU-BURGER",
            "Burger",
            null,
            PartnerCatalogOfferingKind.FnBItemsPerSale,
            Money.Of(25m, vatInclusive: true),
            externalMenuItemId: "JAHEZ-88421");
        item.Publish(DateTime.UtcNow);

        var link = PlatformCatalogLink.CreateDeferredShape2(Guid.NewGuid(), item);

        link.Status.ShouldBe(PlatformCatalogLinkStatus.DeferredShape2);
        link.PlatformVariantId.ShouldBeNull();
        link.PlatformProductId.ShouldBeNull();
        link.PartnerCatalogItemId.ShouldBe(item.Id);
    }

    [Fact]
    public void CreateDeferredShape2_Rejects_Non_FnB_Item()
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

        Should.Throw<BusinessException>(() =>
                PlatformCatalogLink.CreateDeferredShape2(Guid.NewGuid(), item))
            .Code.ShouldBe(PartnerCatalogErrorCodes.InvalidOfferingKind);
    }
}
