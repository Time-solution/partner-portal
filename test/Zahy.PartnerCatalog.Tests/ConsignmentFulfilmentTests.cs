using System;
using Shouldly;
using Volo.Abp;
using Xunit;
using Zahy.PartnerPlatform.Partners;
using Zahy.Settlement;

namespace Zahy.PartnerCatalog;

/// <summary>
/// JUMP / Consignment-Fulfilment domain + config tests. The one new decision —
/// <see cref="ConsignmentOwnershipMode"/> — must route to an EXISTING settlement participation mode.
/// </summary>
public class ConsignmentFulfilmentTests
{
    private static readonly Guid PartnerId = Guid.Parse("44444444-4444-4444-4444-444444444004");

    [Theory]
    [InlineData(ConsignmentOwnershipMode.MerchantOwned, SettlementParticipationMode.ReflectionOnly)]
    [InlineData(ConsignmentOwnershipMode.PartnerBought, SettlementParticipationMode.Principal)]
    public void Ownership_Mode_Routes_To_Existing_Settlement_Mode(
        ConsignmentOwnershipMode ownership,
        SettlementParticipationMode expected)
    {
        ConsignmentOwnershipModeRouting.ResolveParticipationMode(ownership).ShouldBe(expected);
    }

    [Fact]
    public void Missing_Ownership_Mode_Defaults_To_Consignment_ReflectionOnly()
    {
        ConsignmentOwnershipModeRouting.DefaultMode.ShouldBe(ConsignmentOwnershipMode.MerchantOwned);
        ConsignmentOwnershipModeRouting.ResolveParticipationMode((ConsignmentOwnershipMode?)null)
            .ShouldBe(SettlementParticipationMode.ReflectionOnly);
    }

    [Fact]
    public void Create_Consignment_Defaults_To_MerchantOwned_ReflectionOnly()
    {
        var item = CreateConsignmentItem(ownershipMode: null);

        item.OfferingKind.ShouldBe(PartnerCatalogOfferingKind.ConsignmentFulfilment);
        item.ConsignmentOwnershipMode.ShouldBe(ConsignmentOwnershipMode.MerchantOwned);
        item.SettlementParticipationMode.ShouldBe(SettlementParticipationMode.ReflectionOnly);
        item.SettlementTriggerMode.ShouldBe(SettlementTriggerMode.PerOrderLine);
        item.EffectiveSettlementBook.ShouldBe(SettlementBook.Marketplace);
    }

    [Fact]
    public void Create_Consignment_PartnerBought_Routes_To_Principal()
    {
        var item = CreateConsignmentItem(ConsignmentOwnershipMode.PartnerBought);

        item.ConsignmentOwnershipMode.ShouldBe(ConsignmentOwnershipMode.PartnerBought);
        item.SettlementParticipationMode.ShouldBe(SettlementParticipationMode.Principal);
    }

    [Fact]
    public void Create_NonConsignment_With_Ownership_Mode_Is_Rejected()
    {
        Should.Throw<BusinessException>(() => PartnerCatalogItem.Create(
            Guid.NewGuid(),
            PartnerId,
            "SVC-BAD",
            "Service",
            null,
            PartnerCatalogOfferingKind.ServiceOneOff,
            Money.Of(70m, vatInclusive: true),
            consignmentOwnershipMode: ConsignmentOwnershipMode.PartnerBought));
    }

    [Fact]
    public void Consignment_Is_Suggested_For_ThreePL_And_Marketplace()
    {
        PartnerCatalogOfferingKindDefaults.GetSuggestedKinds(PartnerType.ThreePL)
            .ShouldContain(PartnerCatalogOfferingKind.ConsignmentFulfilment);
        PartnerCatalogOfferingKindDefaults.GetSuggestedKinds(PartnerType.Marketplace)
            .ShouldContain(PartnerCatalogOfferingKind.ConsignmentFulfilment);
    }

    private static PartnerCatalogItem CreateConsignmentItem(ConsignmentOwnershipMode? ownershipMode)
    {
        var item = PartnerCatalogItem.Create(
            Guid.NewGuid(),
            PartnerId,
            "JUMP-SKU-1",
            "Consignment SKU",
            null,
            PartnerCatalogOfferingKind.ConsignmentFulfilment,
            Money.Of(25m, vatInclusive: true),
            consignmentOwnershipMode: ownershipMode);
        item.Publish(new DateTime(2026, 6, 20, 0, 0, 0, DateTimeKind.Utc));
        return item;
    }
}
