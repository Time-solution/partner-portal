using System;
using System.Threading.Tasks;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Xunit;
using Zahy.Settlement;

namespace Zahy.PartnerCatalog;

public class PartnerCatalogEfMappingTests : ZahyPartnerCatalogTestBase
{
    [Fact]
    public async Task Should_Persist_Item_And_Reflection_Money_Columns()
    {
        var partnerId = Guid.Parse("22222222-2222-2222-2222-222222222001");

        await WithUnitOfWorkAsync(async () =>
        {
            var itemRepo = GetRequiredService<IRepository<PartnerCatalogItem, Guid>>();
            var reflectionRepo = GetRequiredService<IRepository<PartnerCatalogItemReflection, Guid>>();

            var item = PartnerCatalogItem.Create(
                Guid.NewGuid(),
                partnerId,
                "DLV-STD",
                "Standard delivery",
                null,
                PartnerCatalogOfferingKind.DeliveryFulfilmentPerOrder,
                Money.Of(10m, vatInclusive: true),
                carrierServiceCode: "OTO-STD");
            item.Publish(DateTime.UtcNow);

            await itemRepo.InsertAsync(item, autoSave: true);

            var reflection = PartnerCatalogItemReflection.Create(
                Guid.NewGuid(),
                item.Id,
                partnerId,
                DateTime.UtcNow.AddHours(-1),
                null,
                isPublished: true);

            await reflectionRepo.InsertAsync(reflection, autoSave: true);

            var loaded = await itemRepo.GetAsync(item.Id);
            loaded.PartnerCost.Amount.ShouldBe(10m);
            loaded.PartnerCost.VatInclusive.ShouldBeTrue();
            loaded.CarrierServiceCode.ShouldBe("OTO-STD");

            var loadedReflection = await reflectionRepo.GetAsync(reflection.Id);
            loadedReflection.PartnerId.ShouldBe(partnerId);
        });
    }
}
