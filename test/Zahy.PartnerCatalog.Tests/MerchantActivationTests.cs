using System;
using Shouldly;
using Volo.Abp;
using Xunit;
using Zahy.Settlement;

namespace Zahy.PartnerCatalog;

public class MerchantActivationTests
{
    private static readonly Guid PartnerId = Guid.Parse("22222222-2222-2222-2222-222222222001");
    private static readonly Guid TenantId = Guid.Parse("11111111-1111-1111-1111-111111111001");
    private static readonly DateTime T = new(2026, 6, 2, 14, 30, 0, DateTimeKind.Utc);

    private static PartnerCatalogItem CreateActiveItem() =>
        CreateActiveItemWithParticipation(SettlementParticipationMode.Principal);

    private static PartnerCatalogItem CreateActiveItemWithParticipation(SettlementParticipationMode mode)
    {
        var item = PartnerCatalogItem.Create(
            Guid.NewGuid(),
            PartnerId,
            "SVC-1",
            "Service item",
            null,
            PartnerCatalogOfferingKind.ServiceOneOff,
            Money.Of(70m, vatInclusive: true),
            settlementParticipationMode: mode);
        item.Publish(T);
        return item;
    }

    [Fact]
    public void Create_Starts_Pending_With_Denormalized_PartnerId()
    {
        var item = CreateActiveItem();
        var activation = MerchantActivation.Create(
            Guid.NewGuid(),
            TenantId,
            item,
            Money.Of(100m, vatInclusive: true));

        activation.Status.ShouldBe(MerchantActivationStatus.Pending);
        activation.PartnerId.ShouldBe(PartnerId);
        activation.PartnerCatalogItemId.ShouldBe(item.Id);
        activation.TenantId.ShouldBe(TenantId);
        activation.ResalePrice.Amount.ShouldBe(100m);
        activation.ResalePrice.VatInclusive.ShouldBeTrue();
        activation.IdempotencyKey.ShouldBe(MerchantActivation.BuildIdempotencyKey(TenantId, item.Id));
    }

    [Fact]
    public void Create_Rejects_Non_Vat_Inclusive_ResalePrice()
    {
        var item = CreateActiveItem();

        Should.Throw<BusinessException>(() =>
                MerchantActivation.Create(
                    Guid.NewGuid(),
                    TenantId,
                    item,
                    Money.Of(100m, vatInclusive: false)))
            .Code.ShouldBe(PartnerCatalogErrorCodes.InvalidResalePrice);
    }

    [Fact]
    public void Create_Rejects_Inactive_Catalog_Item()
    {
        var draft = PartnerCatalogItem.Create(
            Guid.NewGuid(),
            PartnerId,
            "DRAFT",
            "Draft",
            null,
            PartnerCatalogOfferingKind.ServiceOneOff,
            Money.Of(70m, vatInclusive: true));

        Should.Throw<BusinessException>(() =>
                MerchantActivation.Create(
                    Guid.NewGuid(),
                    TenantId,
                    draft,
                    Money.Of(100m, vatInclusive: true)))
            .Code.ShouldBe(PartnerCatalogErrorCodes.ItemNotActive);
    }

    [Fact]
    public void ResolveSettlementTriggerMode_Reads_From_Parent_Item_Not_Stored_On_Activation()
    {
        var item = PartnerCatalogItem.Create(
            Guid.NewGuid(),
            PartnerId,
            "DLV",
            "Delivery",
            null,
            PartnerCatalogOfferingKind.DeliveryFulfilmentPerOrder,
            Money.Of(10m, vatInclusive: true));
        item.Publish(T);

        var activation = MerchantActivation.Create(
            Guid.NewGuid(),
            TenantId,
            item,
            Money.Of(15m, vatInclusive: true));

        activation.ResolveSettlementTriggerMode(item).ShouldBe(SettlementTriggerMode.PerOrder);
    }

    [Fact]
    public void ResolveSettlementTriggerMode_Rejects_Mismatched_Item()
    {
        var item = CreateActiveItem();
        var other = CreateActiveItem();
        var activation = MerchantActivation.Create(Guid.NewGuid(), TenantId, item, Money.Of(100m, vatInclusive: true));

        Should.Throw<BusinessException>(() => activation.ResolveSettlementTriggerMode(other))
            .Code.ShouldBe(PartnerCatalogErrorCodes.CatalogItemMismatch);
    }

    [Fact]
    public void Full_Lifecycle_Pending_Active_Suspended_Active_Ended()
    {
        var activation = MerchantActivation.Create(
            Guid.NewGuid(),
            TenantId,
            CreateActiveItem(),
            Money.Of(100m, vatInclusive: true));

        activation.Activate(T);
        activation.Status.ShouldBe(MerchantActivationStatus.Active);
        activation.ActivatedAt.ShouldBe(T);

        activation.Suspend(T.AddHours(1));
        activation.Status.ShouldBe(MerchantActivationStatus.Suspended);

        activation.Resume(T.AddHours(2));
        activation.Status.ShouldBe(MerchantActivationStatus.Active);

        activation.End(T.AddHours(3));
        activation.Status.ShouldBe(MerchantActivationStatus.Ended);
        activation.EndedAt.ShouldBe(T.AddHours(3));
    }

    [Fact]
    public void Cancel_From_Pending_Ends_Without_Activation()
    {
        var activation = MerchantActivation.Create(
            Guid.NewGuid(),
            TenantId,
            CreateActiveItem(),
            Money.Of(100m, vatInclusive: true));

        activation.Cancel(T);
        activation.Status.ShouldBe(MerchantActivationStatus.Ended);
        activation.ActivatedAt.ShouldBeNull();
        activation.EndedAt.ShouldBe(T);
    }

    [Fact]
    public void CatalogItem_Defaults_SettlementParticipationMode_To_Principal()
    {
        var item = PartnerCatalogItem.Create(
            Guid.NewGuid(),
            PartnerId,
            "SVC",
            "Service",
            null,
            PartnerCatalogOfferingKind.ServiceOneOff,
            Money.Of(70m, vatInclusive: true));

        item.SettlementParticipationMode.ShouldBe(SettlementParticipationMode.Principal);
    }
}
