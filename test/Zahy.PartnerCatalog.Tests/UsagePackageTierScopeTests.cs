using System;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using Xunit;
using Zahy.Identity.Permissions;
using Zahy.PartnerCatalog.Packages;
using Zahy.PartnerPlatform.Partners;

namespace Zahy.PartnerCatalog;

/// <summary>
/// U5 — overage tiers are AUDIENCE-SCOPED exactly like the flat overage: admin sees buy + sell + margin,
/// partner sees buy only, merchant (U4 browse) sees sell only. Margin is admin-only and STRUCTURALLY ABSENT.
/// </summary>
public class UsagePackageTierScopeTests : ZahyPartnerCatalogTestBase
{
    private static readonly Guid ServicePartnerId = Guid.Parse("22222222-2222-2222-2222-222222222004");
    private static readonly Guid MerchantTenantId = Guid.Parse("33333333-3333-3333-3333-333333333020");

    private static CreateUsagePackageInput ResaleWithTiers() => new()
    {
        PartnerId = ServicePartnerId,
        Name = "Resale tiered",
        Mode = UsagePackageMode.Resale,
        IncludedQuantity = 5000m,
        BaseBuyAmount = 0m,
        BaseSellAmount = 0m,
        OverageBuyAmount = 0.03m,
        OverageSellAmount = 0.05m,
        Tiers =
        {
            new UsagePackageTierInput { FromQuantity = 0m, ToQuantity = 10000m, BuyRate = 0.03m, SellRate = 0.05m },
            new UsagePackageTierInput { FromQuantity = 10000m, ToQuantity = null, BuyRate = 0.025m, SellRate = 0.04m },
        },
    };

    [Fact]
    public async Task Admin_Sees_Buy_Sell_And_Margin_Tiers()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            GetRequiredService<PartnerCatalogTestCurrentPartner>().Id = null;
            GetRequiredService<PartnerCatalogTestPartnerTypeLookup>().Set(ServicePartnerId, PartnerType.Service);
            GetRequiredService<PartnerCatalogTestPermissionChecker>().GrantOnly(ZahyPermissions.Catalog.AuthorManaged);

            var dto = await GetRequiredService<IUsagePackageWriteAppService>().CreateAsync(ResaleWithTiers());

            dto.Tiers.Count.ShouldBe(2);
            var t0 = dto.Tiers[0];
            t0.BuyRate!.Amount.ShouldBe(0.03m);
            t0.SellRate!.Amount.ShouldBe(0.05m);
            t0.MarginRate!.Amount.ShouldBe(0.02m);
            dto.Tiers[1].ToQuantity.ShouldBeNull(); // open-ended top tier
        });
    }

    [Fact]
    public async Task Partner_Self_View_Sees_Buy_Tiers_Only()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            GetRequiredService<PartnerCatalogTestCurrentPartner>().Id = ServicePartnerId;
            GetRequiredService<PartnerCatalogTestPartnerTypeLookup>().Set(ServicePartnerId, PartnerType.Service);
            GetRequiredService<PartnerCatalogTestPermissionChecker>().GrantOnly(ZahyPermissions.Catalog.AuthorSelf);

            var dto = await GetRequiredService<IUsagePackageWriteAppService>().CreateAsync(ResaleWithTiers());

            var t0 = dto.Tiers[0];
            t0.BuyRate!.Amount.ShouldBe(0.03m);
            t0.SellRate.ShouldBeNull();
            t0.MarginRate.ShouldBeNull();
        });
    }

    [Fact]
    public async Task Merchant_Browse_Sees_Sell_Tiers_Only()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            // Author + publish as admin.
            GetRequiredService<PartnerCatalogTestCurrentPartner>().Id = null;
            GetRequiredService<PartnerCatalogTestPartnerTypeLookup>().Set(ServicePartnerId, PartnerType.Service);
            GetRequiredService<PartnerCatalogTestPermissionChecker>().GrantOnly(ZahyPermissions.Catalog.AuthorManaged);
            var write = GetRequiredService<IUsagePackageWriteAppService>();
            var created = await write.CreateAsync(ResaleWithTiers());
            await write.PublishAsync(created.Id);

            // Browse as the merchant (U4 selection service).
            GetRequiredService<PartnerCatalogTestCurrentPartner>().Id = null;
            var selection = GetRequiredService<IUsagePackageSelectionAppService>();
            var published = await selection.GetPublishedAsync(ServicePartnerId);

            var dto = published.Single(p => p.Id == created.Id);
            var t0 = dto.Tiers[0];
            t0.SellRate!.Amount.ShouldBe(0.05m);
            t0.BuyRate.ShouldBeNull();
            t0.MarginRate.ShouldBeNull();
        });
    }
}
