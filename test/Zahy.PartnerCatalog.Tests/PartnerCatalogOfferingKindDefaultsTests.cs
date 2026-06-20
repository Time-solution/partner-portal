using Shouldly;
using Xunit;
using Zahy.PartnerPlatform.Partners;
using Zahy.Settlement;

namespace Zahy.PartnerCatalog;

public class PartnerCatalogOfferingKindDefaultsTests
{
    [Fact]
    public void Carrier_Defaults_To_DeliveryFulfilmentPerOrder()
    {
        PartnerCatalogOfferingKindDefaults.GetSuggestedKinds(PartnerType.Carrier)
            .ShouldBe(new[] { PartnerCatalogOfferingKind.DeliveryFulfilmentPerOrder });

        PartnerCatalogOfferingKindDefaults.GetDefaultTriggerMode(PartnerCatalogOfferingKind.DeliveryFulfilmentPerOrder)
            .ShouldBe(SettlementTriggerMode.PerOrder);

        PartnerCatalogOfferingKindDefaults.GetDefaultSettlementBook(PartnerCatalogOfferingKind.DeliveryFulfilmentPerOrder)
            .ShouldBe(SettlementBook.Marketplace);
    }

    [Fact]
    public void FnB_Defaults_To_PerOrderLine_And_Marketplace()
    {
        PartnerCatalogOfferingKindDefaults.GetDefaultTriggerMode(PartnerCatalogOfferingKind.FnBItemsPerSale)
            .ShouldBe(SettlementTriggerMode.PerOrderLine);

        PartnerCatalogOfferingKindDefaults.GetDefaultSettlementBook(PartnerCatalogOfferingKind.FnBItemsPerSale)
            .ShouldBe(SettlementBook.Marketplace);
    }
}
