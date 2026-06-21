using System;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.Domain.Repositories;
using Xunit;
using Zahy.PartnerCatalog.Merchant;
using Zahy.Settlement;

namespace Zahy.PartnerCatalog;

public class MerchantCatalogWriteAppServiceTests : ZahyPartnerCatalogTestBase
{
    private static readonly Guid PartnerId = Guid.Parse("22222222-2222-2222-2222-222222222001");
    private static readonly Guid TenantA = Guid.Parse("11111111-1111-1111-1111-111111111001");
    private static readonly Guid TenantB = Guid.Parse("11111111-1111-1111-1111-111111111002");

    [Fact]
    public async Task Activate_Creates_Tenant_Scoped_Active_Instantly()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var item = await InsertActiveItemAsync("MERCH-ACT-1");
            SetTenant(TenantA);

            var merchant = GetRequiredService<IPartnerCatalogMerchantAppService>();
            var dto = await merchant.ActivateAsync(new ActivateMerchantOfferingInput
            {
                PartnerCatalogItemId = item.Id,
            });

            dto.TenantId.ShouldBe(TenantA);
            dto.PartnerCatalogItemId.ShouldBe(item.Id);
            dto.Status.ShouldBe(MerchantActivationStatus.Active);
            dto.ActivatedAt.ShouldNotBeNull();
            dto.ResalePrice.Amount.ShouldBe(70m);
        });
    }

    [Fact]
    public async Task Activate_Is_Idempotent_For_Same_Tenant_And_Offering()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var item = await InsertActiveItemAsync("MERCH-IDEM-1");
            SetTenant(TenantA);

            var merchant = GetRequiredService<IPartnerCatalogMerchantAppService>();
            var first = await merchant.ActivateAsync(new ActivateMerchantOfferingInput
            {
                PartnerCatalogItemId = item.Id,
            });
            var second = await merchant.ActivateAsync(new ActivateMerchantOfferingInput
            {
                PartnerCatalogItemId = item.Id,
            });

            second.Id.ShouldBe(first.Id);

            var activationRepo = GetRequiredService<IRepository<MerchantActivation, Guid>>();
            var count = await activationRepo.CountAsync(x =>
                x.TenantId == TenantA && x.PartnerCatalogItemId == item.Id);
            count.ShouldBe(1);
        });
    }

    [Fact]
    public async Task Activate_Instant_Does_Not_Create_Snapshot()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var item = await InsertActiveItemAsync("MERCH-NOSNAP");
            SetTenant(TenantA);

            var snapshotRepo = GetRequiredService<IRepository<SettlementCostMarkupSnapshot, Guid>>();
            var before = await snapshotRepo.GetCountAsync();

            var merchant = GetRequiredService<IPartnerCatalogMerchantAppService>();
            var dto = await merchant.ActivateAsync(new ActivateMerchantOfferingInput
            {
                PartnerCatalogItemId = item.Id,
            });

            dto.Status.ShouldBe(MerchantActivationStatus.Active);
            var after = await snapshotRepo.GetCountAsync();
            after.ShouldBe(before);
        });
    }

    [Fact]
    public async Task Deactivate_Pending_Moves_To_Ended_Via_Cancel()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var item = await InsertActiveItemAsync("MERCH-DEACT-1");
            SetTenant(TenantA);

            var merchant = GetRequiredService<IPartnerCatalogMerchantAppService>();
            var created = await merchant.ActivateAsync(new ActivateMerchantOfferingInput
            {
                PartnerCatalogItemId = item.Id,
            });

            var ended = await merchant.DeactivateAsync(created.Id);

            ended.Status.ShouldBe(MerchantActivationStatus.Ended);
            ended.EndedAt.ShouldNotBeNull();
        });
    }

    [Fact]
    public async Task Deactivate_Active_Moves_To_Ended()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var item = await InsertActiveItemAsync("MERCH-DEACT-2");
            SetTenant(TenantA);

            var activationRepo = GetRequiredService<IRepository<MerchantActivation, Guid>>();
            var activation = MerchantActivation.Create(
                Guid.NewGuid(),
                TenantA,
                item,
                Money.Of(100m, vatInclusive: true));
            activation.Activate(DateTime.UtcNow);
            await activationRepo.InsertAsync(activation, autoSave: true);

            var merchant = GetRequiredService<IPartnerCatalogMerchantAppService>();
            var ended = await merchant.DeactivateAsync(activation.Id);

            ended.Status.ShouldBe(MerchantActivationStatus.Ended);
            ended.EndedAt.ShouldNotBeNull();
        });
    }

    [Fact]
    public async Task Deactivate_Ended_Is_Blocked_By_State_Machine()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var item = await InsertActiveItemAsync("MERCH-DEACT-3");
            SetTenant(TenantA);

            var merchant = GetRequiredService<IPartnerCatalogMerchantAppService>();
            var created = await merchant.ActivateAsync(new ActivateMerchantOfferingInput
            {
                PartnerCatalogItemId = item.Id,
            });
            await merchant.DeactivateAsync(created.Id);

            var ex = await Should.ThrowAsync<BusinessException>(() =>
                merchant.DeactivateAsync(created.Id));
            ex.Code.ShouldBe(PartnerCatalogErrorCodes.InvalidStatusTransition);
        });
    }

    [Fact]
    public async Task Cross_Tenant_Deactivate_Is_Blocked()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var item = await InsertActiveItemAsync("MERCH-XT-1");
            SetTenant(TenantA);

            var merchant = GetRequiredService<IPartnerCatalogMerchantAppService>();
            var created = await merchant.ActivateAsync(new ActivateMerchantOfferingInput
            {
                PartnerCatalogItemId = item.Id,
            });

            SetTenant(TenantB);

            await Should.ThrowAsync<AbpAuthorizationException>(() =>
                merchant.DeactivateAsync(created.Id));
        });
    }

    [Fact]
    public async Task Cross_Tenant_Activate_Query_Is_Blocked()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            SetTenant(TenantA);
            var merchant = GetRequiredService<IPartnerCatalogMerchantAppService>();

            await Should.ThrowAsync<AbpAuthorizationException>(() =>
                merchant.GetMyActivationsAsync(new MerchantActivationsForTenantQuery
                {
                    TenantId = TenantB,
                }));
        });
    }

    [Fact]
    public async Task GetAvailableOfferings_Returns_Active_Catalog_Items()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var active = await InsertActiveItemAsync("MERCH-BROWSE-1");
            await InsertDraftItemAsync("MERCH-BROWSE-DRAFT");
            SetTenant(TenantA);

            var merchant = GetRequiredService<IPartnerCatalogMerchantAppService>();
            var offerings = await merchant.GetAvailableOfferingsAsync();

            offerings.Count.ShouldBe(1);
            offerings.Single().Id.ShouldBe(active.Id);
            offerings.Single().Code.ShouldBe("MERCH-BROWSE-1");
        });
    }

    [Fact]
    public async Task Activate_After_Ended_Is_Blocked()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var item = await InsertActiveItemAsync("MERCH-REACT-1");
            SetTenant(TenantA);

            var merchant = GetRequiredService<IPartnerCatalogMerchantAppService>();
            var created = await merchant.ActivateAsync(new ActivateMerchantOfferingInput
            {
                PartnerCatalogItemId = item.Id,
            });
            await merchant.DeactivateAsync(created.Id);

            var ex = await Should.ThrowAsync<BusinessException>(() =>
                merchant.ActivateAsync(new ActivateMerchantOfferingInput
                {
                    PartnerCatalogItemId = item.Id,
                }));
            ex.Code.ShouldBe(PartnerCatalogErrorCodes.ActivationAlreadyEnded);
        });
    }

    private void SetTenant(Guid tenantId) =>
        GetRequiredService<PartnerCatalogTestCurrentTenant>().Id = tenantId;

    private async Task<PartnerCatalogItem> InsertActiveItemAsync(string code) =>
        await InsertItemInternalAsync(code, publish: true);

    private Task InsertDraftItemAsync(string code) =>
        InsertItemInternalAsync(code, publish: false);

    private async Task<PartnerCatalogItem> InsertItemInternalAsync(string code, bool publish)
    {
        var itemRepo = GetRequiredService<IRepository<PartnerCatalogItem, Guid>>();
        var item = PartnerCatalogItem.Create(
            Guid.NewGuid(),
            PartnerId,
            code,
            code,
            null,
            PartnerCatalogOfferingKind.ServiceOneOff,
            Money.Of(70m, vatInclusive: true));
        if (publish)
        {
            item.Publish(DateTime.UtcNow);
        }

        await itemRepo.InsertAsync(item, autoSave: true);
        return item;
    }
}
