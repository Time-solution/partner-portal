using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Xunit;
using Zahy.Settlement;

namespace Zahy.PartnerCatalog;

public class SettlementCostMarkupSnapshotEfTests : ZahyPartnerCatalogTestBase
{
    private static readonly Guid PartnerId = Guid.Parse("22222222-2222-2222-2222-222222222001");
    private static readonly Guid TenantId = Guid.Parse("11111111-1111-1111-1111-111111111001");

    [Fact]
    public async Task Should_Persist_Money_Columns_And_Null_SettlementCaseId()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var (item, activation) = await SeedServiceActivationAsync();
            var repo = GetRequiredService<IRepository<SettlementCostMarkupSnapshot, Guid>>();

            var snapshot = SettlementCostMarkupSnapshot.Create(
                Guid.NewGuid(),
                activation,
                item,
                Money.Of(100m, vatInclusive: true),
                SettlementCostMarkupTrigger.Activation,
                "pcat:activation:ef:v1");

            await repo.InsertAsync(snapshot, autoSave: true);

            var loaded = await repo.GetAsync(snapshot.Id);
            loaded.BuyPrice.Amount.ShouldBe(70m);
            loaded.SellPrice.Amount.ShouldBe(100m);
            loaded.SettlementCaseId.ShouldBeNull();
            loaded.OrderLineId.ShouldBe(string.Empty);
        });
    }

    [Fact]
    public async Task Same_ExternalTxn_Different_OrderLineIds_Do_Not_Collide()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var (item, activation) = await SeedFnBActivationAsync();
            var repo = GetRequiredService<IRepository<SettlementCostMarkupSnapshot, Guid>>();
            const string ext = "order:ord-9001:v2";

            await repo.InsertAsync(
                SettlementCostMarkupSnapshot.Create(
                    Guid.NewGuid(),
                    activation,
                    item,
                    Money.Of(40m, vatInclusive: true),
                    SettlementCostMarkupTrigger.OrderLine,
                    ext,
                    orderLineId: "ORD-9001-L1"),
                autoSave: true);

            await repo.InsertAsync(
                SettlementCostMarkupSnapshot.Create(
                    Guid.NewGuid(),
                    activation,
                    item,
                    Money.Of(40m, vatInclusive: true),
                    SettlementCostMarkupTrigger.OrderLine,
                    ext,
                    orderLineId: "ORD-9001-L2"),
                autoSave: true);

            (await repo.GetCountAsync()).ShouldBe(2);
        });
    }

    [Fact]
    public async Task Same_ExternalTxn_Both_Empty_OrderLineIds_Collide_On_Unique_Index()
    {
        PartnerCatalogItem item = null!;
        MerchantActivation activation = null!;
        const string ext = "pcat:order:ord-dup:v1";

        await WithUnitOfWorkAsync(async () =>
        {
            (item, activation) = await SeedServiceActivationAsync();
            var repo = GetRequiredService<IRepository<SettlementCostMarkupSnapshot, Guid>>();

            await repo.InsertAsync(
                SettlementCostMarkupSnapshot.Create(
                    Guid.NewGuid(),
                    activation,
                    item,
                    Money.Of(100m, vatInclusive: true),
                    SettlementCostMarkupTrigger.Order,
                    ext),
                autoSave: true);
        });

        var caught = false;
        try
        {
            await WithUnitOfWorkAsync(async () =>
            {
                var repo = GetRequiredService<IRepository<SettlementCostMarkupSnapshot, Guid>>();
                await repo.InsertAsync(
                    SettlementCostMarkupSnapshot.Create(
                        Guid.NewGuid(),
                        activation,
                        item,
                        Money.Of(100m, vatInclusive: true),
                        SettlementCostMarkupTrigger.Activation,
                        ext),
                    autoSave: true);
            });
        }
        catch (DbUpdateException)
        {
            caught = true;
        }

        caught.ShouldBeTrue();
    }

    [Fact]
    public async Task Should_Persist_PlatformCatalogLink_DeferredShape2()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var itemRepo = GetRequiredService<IRepository<PartnerCatalogItem, Guid>>();
            var linkRepo = GetRequiredService<IRepository<PlatformCatalogLink, Guid>>();

            var item = PartnerCatalogItem.Create(
                Guid.NewGuid(),
                PartnerId,
                "MENU-EF",
                "Burger",
                null,
                PartnerCatalogOfferingKind.FnBItemsPerSale,
                Money.Of(25m, vatInclusive: true),
                externalMenuItemId: "JAHEZ-EF");
            item.Publish(DateTime.UtcNow);
            await itemRepo.InsertAsync(item, autoSave: true);

            var link = PlatformCatalogLink.CreateDeferredShape2(Guid.NewGuid(), item);
            await linkRepo.InsertAsync(link, autoSave: true);

            var loaded = await linkRepo.GetAsync(link.Id);
            loaded.Status.ShouldBe(PlatformCatalogLinkStatus.DeferredShape2);
            loaded.PlatformVariantId.ShouldBeNull();
        });
    }

    private async Task<(PartnerCatalogItem Item, MerchantActivation Activation)> SeedServiceActivationAsync()
    {
        var itemRepo = GetRequiredService<IRepository<PartnerCatalogItem, Guid>>();
        var activationRepo = GetRequiredService<IRepository<MerchantActivation, Guid>>();

        var item = PartnerCatalogItem.Create(
            Guid.NewGuid(),
            PartnerId,
            "SVC-EF",
            "Service",
            null,
            PartnerCatalogOfferingKind.ServiceOneOff,
            Money.Of(70m, vatInclusive: true));
        item.Publish(DateTime.UtcNow);
        await itemRepo.InsertAsync(item, autoSave: true);

        var activation = MerchantActivation.Create(
            Guid.NewGuid(),
            TenantId,
            item,
            Money.Of(100m, vatInclusive: true));
        activation.Activate(DateTime.UtcNow);
        await activationRepo.InsertAsync(activation, autoSave: true);

        return (item, activation);
    }

    private async Task<(PartnerCatalogItem Item, MerchantActivation Activation)> SeedFnBActivationAsync()
    {
        var itemRepo = GetRequiredService<IRepository<PartnerCatalogItem, Guid>>();
        var activationRepo = GetRequiredService<IRepository<MerchantActivation, Guid>>();

        var item = PartnerCatalogItem.Create(
            Guid.NewGuid(),
            PartnerId,
            "MENU-DUP",
            "Burger",
            null,
            PartnerCatalogOfferingKind.FnBItemsPerSale,
            Money.Of(25m, vatInclusive: true),
            externalMenuItemId: "JAHEZ-DUP");
        item.Publish(DateTime.UtcNow);
        await itemRepo.InsertAsync(item, autoSave: true);

        var activation = MerchantActivation.Create(
            Guid.NewGuid(),
            TenantId,
            item,
            Money.Of(35m, vatInclusive: true));
        activation.Activate(DateTime.UtcNow);
        await activationRepo.InsertAsync(activation, autoSave: true);

        return (item, activation);
    }
}
