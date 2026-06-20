using System;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using Volo.Abp.Authorization;
using Volo.Abp.Domain.Repositories;
using Xunit;
using Zahy.Settlement;

namespace Zahy.PartnerCatalog.Read;

public class PartnerCatalogReadAppServiceTests : ZahyPartnerCatalogTestBase
{
    private static readonly Guid PartnerA = Guid.Parse("22222222-2222-2222-2222-222222222001");
    private static readonly Guid PartnerB = Guid.Parse("33333333-3333-3333-3333-333333333003");
    private static readonly Guid TenantA = Guid.Parse("11111111-1111-1111-1111-111111111001");

    [Fact]
    public async Task Should_Map_Catalog_Item_With_Real_Domain_Fields()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var itemRepo = GetRequiredService<IRepository<PartnerCatalogItem, Guid>>();
            var item = PartnerCatalogItem.Create(
                Guid.NewGuid(),
                PartnerA,
                "DEL-STD",
                "Standard delivery",
                "Desc",
                PartnerCatalogOfferingKind.DeliveryFulfilmentPerOrder,
                Money.Of(10m, vatInclusive: true),
                settlementParticipationMode: SettlementParticipationMode.Principal);
            item.Publish(DateTime.UtcNow);
            await itemRepo.InsertAsync(item, autoSave: true);

            var read = GetRequiredService<IPartnerCatalogReadAppService>();
            var dtos = await read.GetCatalogItemsAsync(new PartnerCatalogItemsQuery { PartnerId = PartnerA });

            dtos.Count.ShouldBe(1);
            var dto = dtos.Single();
            dto.OfferingKind.ShouldBe(PartnerCatalogOfferingKind.DeliveryFulfilmentPerOrder);
            dto.SettlementParticipationMode.ShouldBe(SettlementParticipationMode.Principal);
            dto.SettlementTriggerMode.ShouldBe(item.SettlementTriggerMode);
            dto.DefaultVatTreatment.ShouldBe(VatTreatment.Principal);
            dto.PartnerCost.Amount.ShouldBe(10m);
            dto.PartnerCost.VatInclusive.ShouldBeTrue();
        });
    }

    [Fact]
    public async Task Should_Map_Activation_Status_Only()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var item = await InsertActiveItemAsync(PartnerA, "ACT-1", PartnerCatalogOfferingKind.ServiceOneOff);
            var activationRepo = GetRequiredService<IRepository<MerchantActivation, Guid>>();
            var activation = MerchantActivation.Create(
                Guid.NewGuid(),
                TenantA,
                item,
                Money.Of(115m, vatInclusive: true));
            activation.Activate(DateTime.UtcNow);
            await activationRepo.InsertAsync(activation, autoSave: true);

            var read = GetRequiredService<IPartnerCatalogReadAppService>();
            var dtos = await read.GetActivationsAsync(new MerchantActivationsQuery { PartnerId = PartnerA });

            dtos.Count.ShouldBe(1);
            dtos.Single().Status.ShouldBe(MerchantActivationStatus.Active);
            dtos.Single().ResalePrice.Amount.ShouldBe(115m);
        });
    }

    [Fact]
    public async Task Partner_Filter_Blocks_Cross_Partner_Read()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var itemA = await InsertActiveItemAsync(PartnerA, "A-1", PartnerCatalogOfferingKind.ServiceOneOff);
            var itemB = await InsertActiveItemAsync(PartnerB, "B-1", PartnerCatalogOfferingKind.ServiceOneOff);
            var activationRepo = GetRequiredService<IRepository<MerchantActivation, Guid>>();
            await activationRepo.InsertAsync(
                MerchantActivation.Create(Guid.NewGuid(), TenantA, itemA, Money.Of(100m, vatInclusive: true)),
                autoSave: true);
            await activationRepo.InsertAsync(
                MerchantActivation.Create(Guid.NewGuid(), TenantA, itemB, Money.Of(100m, vatInclusive: true)),
                autoSave: true);

            var currentPartner = GetRequiredService<PartnerCatalogTestCurrentPartner>();
            currentPartner.Id = PartnerA;

            var read = GetRequiredService<IPartnerCatalogReadAppService>();
            var visible = await read.GetActivationsAsync(new MerchantActivationsQuery());
            visible.Count.ShouldBe(1);
            visible.Single().PartnerId.ShouldBe(PartnerA);

            await Should.ThrowAsync<AbpAuthorizationException>(() =>
                read.GetActivationsAsync(new MerchantActivationsQuery { PartnerId = PartnerB }));
        });
    }

    private async Task<PartnerCatalogItem> InsertActiveItemAsync(
        Guid partnerId,
        string code,
        PartnerCatalogOfferingKind kind)
    {
        var itemRepo = GetRequiredService<IRepository<PartnerCatalogItem, Guid>>();
        var item = PartnerCatalogItem.Create(
            Guid.NewGuid(),
            partnerId,
            code,
            code,
            null,
            kind,
            Money.Of(70m, vatInclusive: true));
        item.Publish(DateTime.UtcNow);
        await itemRepo.InsertAsync(item, autoSave: true);
        return item;
    }
}
