using System;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Xunit;
using Zahy.Identity.Permissions;
using Zahy.PartnerCatalog.Packages;
using Zahy.PartnerPlatform.Partners;
using Zahy.Settlement;

namespace Zahy.PartnerCatalog;

/// <summary>
/// U4 — merchant package browse + selection (instant activation). Verifies browse shows ONLY published
/// packages on the merchant SELL view (buy/margin absent), selecting records an Active link instantly,
/// drafts cannot be selected, mid-cycle end works, and NOTHING is posted (flags OFF, no snapshot).
/// </summary>
public class UsagePackageSelectionAppServiceTests : ZahyPartnerCatalogTestBase
{
    private static readonly Guid ServicePartnerId = Guid.Parse("22222222-2222-2222-2222-222222222004");
    private static readonly Guid MerchantTenantId = Guid.Parse("33333333-3333-3333-3333-333333333010");

    private void AsAdminAuthor()
    {
        GetRequiredService<PartnerCatalogTestCurrentPartner>().Id = null;
        GetRequiredService<PartnerCatalogTestPartnerTypeLookup>().Set(ServicePartnerId, PartnerType.Service);
        GetRequiredService<PartnerCatalogTestPermissionChecker>().GrantOnly(ZahyPermissions.Catalog.AuthorManaged);
    }

    private void AsMerchant()
    {
        // A merchant is NOT a partner — clear the current partner so the partner query filter is disabled.
        GetRequiredService<PartnerCatalogTestCurrentPartner>().Id = null;
        GetRequiredService<PartnerCatalogTestPermissionChecker>().GrantOnly(string.Empty);
    }

    private async Task<(Guid resaleId, Guid subId)> SeedPublishedAndDraftAsync()
    {
        AsAdminAuthor();
        var write = GetRequiredService<IUsagePackageWriteAppService>();

        var resale = await write.CreateAsync(new CreateUsagePackageInput
        {
            PartnerId = ServicePartnerId,
            Name = "Resale 10k",
            UnitLabel = "messages",
            Mode = UsagePackageMode.Resale,
            IncludedQuantity = 10000m,
            BaseBuyAmount = 200m,
            BaseSellAmount = 300m,
            OverageBuyAmount = 0.02m,
            OverageSellAmount = 0.05m,
        });
        await write.PublishAsync(resale.Id);

        var sub = await write.CreateAsync(new CreateUsagePackageInput
        {
            PartnerId = ServicePartnerId,
            Name = "Subscription",
            Mode = UsagePackageMode.Subscription,
            IncludedQuantity = 5000m,
            BaseSellAmount = 149m,
            OverageSellAmount = 0.03m,
            Payer = ActivationFeePayer.Merchant,
        });
        await write.PublishAsync(sub.Id);

        // A draft package — must NOT be browsable/selectable.
        await write.CreateAsync(new CreateUsagePackageInput
        {
            PartnerId = ServicePartnerId,
            Name = "Draft (hidden)",
            Mode = UsagePackageMode.Resale,
            BaseSellAmount = 99m,
        });

        return (resale.Id, sub.Id);
    }

    [Fact]
    public async Task Merchant_Browses_Only_Published_Packages_On_The_Sell_View()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            await SeedPublishedAndDraftAsync();

            AsMerchant();
            var svc = GetRequiredService<IUsagePackageSelectionAppService>();
            var published = await svc.GetPublishedAsync(ServicePartnerId);

            // Only the 2 PUBLISHED packages are visible — the draft is excluded.
            published.Count.ShouldBe(2);
            published.ShouldAllBe(p => p.Status == UsagePackageStatus.Published);

            // Merchant view: SELL/fee shown, BUY + MARGIN structurally absent.
            var resale = published.Single(p => p.Mode == UsagePackageMode.Resale);
            resale.Audience.ShouldBe(UsagePackageAudience.Merchant);
            resale.BaseSell!.Amount.ShouldBe(300m);
            resale.OverageSell!.Amount.ShouldBe(0.05m);
            resale.BaseBuy.ShouldBeNull();
            resale.BaseMargin.ShouldBeNull();
            resale.OverageMargin.ShouldBeNull();

            var sub = published.Single(p => p.Mode == UsagePackageMode.Subscription);
            sub.BaseSell!.Amount.ShouldBe(149m);
            sub.Payer.ShouldBe(ActivationFeePayer.Merchant);
            sub.BaseBuy.ShouldBeNull();
        });
    }

    [Fact]
    public async Task Selecting_A_Published_Package_Records_An_Active_Selection_Instantly()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var (resaleId, _) = await SeedPublishedAndDraftAsync();

            AsMerchant();
            var svc = GetRequiredService<IUsagePackageSelectionAppService>();
            var selection = await svc.SelectAsync(new SelectUsagePackageInput
            {
                PartnerId = ServicePartnerId,
                UsagePackageId = resaleId,
                TenantId = MerchantTenantId,
                MerchantName = "Pizza House",
            });

            // Instant — Active immediately, no approval; carries the active window start.
            selection.Status.ShouldBe(UsagePackageSelectionStatus.Active);
            selection.UsagePackageId.ShouldBe(resaleId);
            selection.TenantId.ShouldBe(MerchantTenantId);
            selection.EndedAt.ShouldBeNull();

            var mine = await svc.GetForMerchantAsync(MerchantTenantId);
            mine.Count.ShouldBe(1);
            mine[0].UsagePackageId.ShouldBe(resaleId);
        });
    }

    [Fact]
    public async Task Cannot_Select_A_Draft_Or_Unknown_Package()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            await SeedPublishedAndDraftAsync();
            var draft = (await GetRequiredService<IRepository<UsagePackage, Guid>>().GetListAsync())
                .Single(p => p.Name == "Draft (hidden)");

            AsMerchant();
            var svc = GetRequiredService<IUsagePackageSelectionAppService>();

            var notPublished = await Should.ThrowAsync<BusinessException>(() => svc.SelectAsync(new SelectUsagePackageInput
            {
                PartnerId = ServicePartnerId,
                UsagePackageId = draft.Id,
                TenantId = MerchantTenantId,
                MerchantName = "Pizza House",
            }));
            notPublished.Code.ShouldBe(PartnerCatalogErrorCodes.UsagePackageNotPublished);

            var unknown = await Should.ThrowAsync<BusinessException>(() => svc.SelectAsync(new SelectUsagePackageInput
            {
                PartnerId = ServicePartnerId,
                UsagePackageId = Guid.NewGuid(),
                TenantId = MerchantTenantId,
                MerchantName = "Pizza House",
            }));
            unknown.Code.ShouldBe(PartnerCatalogErrorCodes.UsagePackageNotFound);
        });
    }

    [Fact]
    public async Task Mid_Cycle_End_Closes_The_Active_Window()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var (resaleId, _) = await SeedPublishedAndDraftAsync();

            AsMerchant();
            var svc = GetRequiredService<IUsagePackageSelectionAppService>();
            var selection = await svc.SelectAsync(new SelectUsagePackageInput
            {
                PartnerId = ServicePartnerId,
                UsagePackageId = resaleId,
                TenantId = MerchantTenantId,
                MerchantName = "Pizza House",
            });

            var ended = await svc.EndAsync(selection.Id);
            ended.Status.ShouldBe(UsagePackageSelectionStatus.Ended);
            ended.EndedAt.ShouldNotBeNull();
        });
    }

    [Fact]
    public async Task Selection_Does_Not_Post_Or_Create_Snapshot()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var (resaleId, _) = await SeedPublishedAndDraftAsync();
            var snapshotRepo = GetRequiredService<IRepository<SettlementCostMarkupSnapshot, Guid>>();
            var before = await snapshotRepo.GetCountAsync();

            AsMerchant();
            var svc = GetRequiredService<IUsagePackageSelectionAppService>();
            var selection = await svc.SelectAsync(new SelectUsagePackageInput
            {
                PartnerId = ServicePartnerId,
                UsagePackageId = resaleId,
                TenantId = MerchantTenantId,
                MerchantName = "Pizza House",
            });
            await svc.EndAsync(selection.Id);

            // SELECTION/LINK ONLY — no settlement snapshot, no journal posted.
            (await snapshotRepo.GetCountAsync()).ShouldBe(before);
        });
    }
}
