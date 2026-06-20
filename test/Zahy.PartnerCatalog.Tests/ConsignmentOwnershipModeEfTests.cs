using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Xunit;
using Zahy.Settlement;

namespace Zahy.PartnerCatalog;

public class ConsignmentOwnershipModeEfTests : ZahyPartnerCatalogTestBase
{
    private static readonly Guid PartnerId = Guid.Parse("44444444-4444-4444-4444-444444444004");

    [Fact]
    public async Task Should_Persist_ConsignmentOwnershipMode_On_Item()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var itemRepo = GetRequiredService<IRepository<PartnerCatalogItem, Guid>>();
            var item = PartnerCatalogItem.Create(
                Guid.NewGuid(),
                PartnerId,
                "JUMP-EF",
                "Consignment SKU",
                null,
                PartnerCatalogOfferingKind.ConsignmentFulfilment,
                Money.Of(25m, vatInclusive: true),
                consignmentOwnershipMode: ConsignmentOwnershipMode.PartnerBought);
            item.Publish(DateTime.UtcNow);
            await itemRepo.InsertAsync(item, autoSave: true);

            var loaded = await itemRepo.GetAsync(item.Id);
            loaded.ConsignmentOwnershipMode.ShouldBe(ConsignmentOwnershipMode.PartnerBought);
            loaded.SettlementParticipationMode.ShouldBe(SettlementParticipationMode.Principal);
        });
    }

    [Fact]
    public async Task NonConsignment_Item_Has_Null_OwnershipMode()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var itemRepo = GetRequiredService<IRepository<PartnerCatalogItem, Guid>>();
            var item = PartnerCatalogItem.Create(
                Guid.NewGuid(),
                PartnerId,
                "SVC-EF-NULL",
                "Service",
                null,
                PartnerCatalogOfferingKind.ServiceOneOff,
                Money.Of(70m, vatInclusive: true));
            item.Publish(DateTime.UtcNow);
            await itemRepo.InsertAsync(item, autoSave: true);

            var loaded = await itemRepo.GetAsync(item.Id);
            loaded.ConsignmentOwnershipMode.ShouldBeNull();
        });
    }
}
