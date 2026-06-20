using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Xunit;
using Zahy.Identity.Partners;
using Zahy.Settlement;

namespace Zahy.PartnerCatalog;

public class MerchantActivationEfMappingTests : ZahyPartnerCatalogTestBase
{
    private static readonly Guid PartnerA = Guid.Parse("22222222-2222-2222-2222-222222222001");
    private static readonly Guid PartnerB = Guid.Parse("33333333-3333-3333-3333-333333333003");
    private static readonly Guid TenantA = Guid.Parse("11111111-1111-1111-1111-111111111001");

    [Fact]
    public async Task Should_Persist_Activation_Money_And_TenantId()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var item = await InsertActiveItemAsync(PartnerA, "SVC-EF", PartnerCatalogOfferingKind.ServiceOneOff);

            var activationRepo = GetRequiredService<IRepository<MerchantActivation, Guid>>();
            var activation = MerchantActivation.Create(
                Guid.NewGuid(),
                TenantA,
                item,
                Money.Of(100m, vatInclusive: true),
                externalReference: "PO-42");
            activation.Activate(DateTime.UtcNow);

            await activationRepo.InsertAsync(activation, autoSave: true);

            var loaded = await activationRepo.GetAsync(activation.Id);
            loaded.TenantId.ShouldBe(TenantA);
            loaded.PartnerId.ShouldBe(PartnerA);
            loaded.ResalePrice.Amount.ShouldBe(100m);
            loaded.ResalePrice.VatInclusive.ShouldBeTrue();
            loaded.ExternalReference.ShouldBe("PO-42");
        });
    }

    [Fact]
    public async Task Partner_Filter_Hides_Other_Partners_Activations()
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

            var dataFilter = GetRequiredService<IDataFilter>();
            var currentPartner = GetRequiredService<PartnerCatalogTestCurrentPartner>();

            using (dataFilter.Disable<IPartnerCatalogDataFilter>())
            {
                (await activationRepo.GetCountAsync()).ShouldBe(2);
            }

            currentPartner.Id = PartnerA;
            using (dataFilter.Enable<IPartnerCatalogDataFilter>())
            {
                var visible = await activationRepo.GetListAsync();
                visible.Count.ShouldBe(1);
                visible.Single().PartnerId.ShouldBe(PartnerA);
            }
        });
    }

    [Fact]
    public async Task Should_Persist_SettlementParticipationMode_On_Item()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var itemRepo = GetRequiredService<IRepository<PartnerCatalogItem, Guid>>();
            var item = PartnerCatalogItem.Create(
                Guid.NewGuid(),
                PartnerA,
                "REF-ONLY",
                "Reflection only",
                null,
                PartnerCatalogOfferingKind.ServiceOneOff,
                Money.Of(70m, vatInclusive: true),
                settlementParticipationMode: SettlementParticipationMode.ReflectionOnly);
            item.Publish(DateTime.UtcNow);
            await itemRepo.InsertAsync(item, autoSave: true);

            var loaded = await itemRepo.GetAsync(item.Id);
            loaded.SettlementParticipationMode.ShouldBe(SettlementParticipationMode.ReflectionOnly);
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
