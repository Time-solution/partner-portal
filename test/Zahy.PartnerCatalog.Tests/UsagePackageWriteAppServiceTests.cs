using System;
using System.Threading.Tasks;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.Domain.Repositories;
using Xunit;
using Zahy.Identity.Permissions;
using Zahy.PartnerCatalog.Packages;
using Zahy.PartnerPlatform.Partners;
using Zahy.Settlement;

namespace Zahy.PartnerCatalog;

public class UsagePackageWriteAppServiceTests : ZahyPartnerCatalogTestBase
{
    private static readonly Guid ServicePartnerId = Guid.Parse("22222222-2222-2222-2222-222222222004");
    private static readonly Guid DeliveryPartnerId = Guid.Parse("22222222-2222-2222-2222-222222222001");

    [Fact]
    public async Task Service_Partner_Creates_Publishes_Both_Modes_And_Multiple_Packages()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            ConfigureSelfServiceAuthor(ServicePartnerId, PartnerType.Service);
            var write = GetRequiredService<IUsagePackageWriteAppService>();

            var resale = await write.CreateAsync(new CreateUsagePackageInput
            {
                PartnerId = ServicePartnerId,
                Name = "WhatsApp resale 10k",
                UnitLabel = "messages",
                Mode = UsagePackageMode.Resale,
                IncludedQuantity = 10000m,
                BaseBuyAmount = 200m,
                BaseSellAmount = 300m,
                OverageBuyAmount = 0.02m,
                OverageSellAmount = 0.05m,
            });
            resale.Status.ShouldBe(UsagePackageStatus.Draft);
            resale.Mode.ShouldBe(UsagePackageMode.Resale);

            var subscription = await write.CreateAsync(new CreateUsagePackageInput
            {
                PartnerId = ServicePartnerId,
                Name = "WhatsApp subscription",
                UnitLabel = "messages",
                Mode = UsagePackageMode.Subscription,
                IncludedQuantity = 5000m,
                BaseSellAmount = 149m,
                OverageSellAmount = 0.03m,
                Payer = ActivationFeePayer.Merchant,
            });
            subscription.Mode.ShouldBe(UsagePackageMode.Subscription);

            var publishedResale = await write.PublishAsync(resale.Id);
            publishedResale.Status.ShouldBe(UsagePackageStatus.Published);

            var list = await write.GetListAsync(new UsagePackagesQuery { PartnerId = ServicePartnerId });
            list.Count.ShouldBe(2);
        });
    }

    [Fact]
    public async Task Resale_Package_Carries_Buy_Sell_For_Admin()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            ConfigureManagedAuthor(ServicePartnerId, PartnerType.Service);
            var write = GetRequiredService<IUsagePackageWriteAppService>();

            var dto = await write.CreateAsync(new CreateUsagePackageInput
            {
                PartnerId = ServicePartnerId,
                Name = "Resale buy/sell",
                Mode = UsagePackageMode.Resale,
                IncludedQuantity = 1000m,
                BaseBuyAmount = 80m,
                BaseSellAmount = 120m,
                OverageBuyAmount = 0.01m,
                OverageSellAmount = 0.04m,
            });

            // Admin audience sees buy, sell AND margin.
            dto.Audience.ShouldBe(UsagePackageAudience.Admin);
            dto.BaseBuy!.Amount.ShouldBe(80m);
            dto.BaseSell!.Amount.ShouldBe(120m);
            dto.BaseMargin!.Amount.ShouldBe(40m);
            dto.OverageMargin!.Amount.ShouldBe(0.03m);
            dto.Payer.ShouldBeNull();
        });
    }

    [Fact]
    public async Task Subscription_Package_Carries_Fee_And_Payer_No_Buy_Or_Margin()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            ConfigureManagedAuthor(ServicePartnerId, PartnerType.Service);
            var write = GetRequiredService<IUsagePackageWriteAppService>();

            var dto = await write.CreateAsync(new CreateUsagePackageInput
            {
                PartnerId = ServicePartnerId,
                Name = "Subscription fee",
                Mode = UsagePackageMode.Subscription,
                IncludedQuantity = 5000m,
                BaseBuyAmount = 999m, // must be ignored — subscription has no partner payout
                BaseSellAmount = 149m,
                OverageSellAmount = 0.03m,
                Payer = ActivationFeePayer.Partner,
            });

            dto.BaseSell!.Amount.ShouldBe(149m);
            dto.OverageSell!.Amount.ShouldBe(0.03m);
            dto.Payer.ShouldBe(ActivationFeePayer.Partner);
            dto.BaseBuy.ShouldBeNull();
            dto.BaseMargin.ShouldBeNull();
            dto.OverageMargin.ShouldBeNull();
        });
    }

    [Fact]
    public async Task Partner_Self_Author_Render_Has_No_Sell_Or_Margin_For_Resale()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            ConfigureSelfServiceAuthor(ServicePartnerId, PartnerType.Service);
            var write = GetRequiredService<IUsagePackageWriteAppService>();

            var dto = await write.CreateAsync(new CreateUsagePackageInput
            {
                PartnerId = ServicePartnerId,
                Name = "Resale partner view",
                Mode = UsagePackageMode.Resale,
                IncludedQuantity = 1000m,
                BaseBuyAmount = 80m,
                BaseSellAmount = 120m,
                OverageBuyAmount = 0.01m,
                OverageSellAmount = 0.04m,
            });

            // Partner sees BUY only — sell + margin are STRUCTURALLY ABSENT.
            dto.Audience.ShouldBe(UsagePackageAudience.Partner);
            dto.BaseBuy!.Amount.ShouldBe(80m);
            dto.OverageBuy!.Amount.ShouldBe(0.01m);
            dto.BaseSell.ShouldBeNull();
            dto.OverageSell.ShouldBeNull();
            dto.BaseMargin.ShouldBeNull();
            dto.OverageMargin.ShouldBeNull();
        });
    }

    [Fact]
    public async Task Delivery_Partner_Cannot_Self_Author_Packages()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            ConfigureSelfServiceAuthor(DeliveryPartnerId, PartnerType.Carrier);
            var write = GetRequiredService<IUsagePackageWriteAppService>();

            var ex = await Should.ThrowAsync<BusinessException>(() =>
                write.CreateAsync(new CreateUsagePackageInput
                {
                    PartnerId = DeliveryPartnerId,
                    Name = "Illegal delivery package",
                    Mode = UsagePackageMode.Resale,
                }));

            ex.Code.ShouldBe(PartnerCatalogErrorCodes.AuthoringNotPermitted);
        });
    }

    [Fact]
    public async Task Admin_Can_Author_Any_Partner_Package()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            ConfigureManagedAuthor(DeliveryPartnerId, PartnerType.Carrier);
            var write = GetRequiredService<IUsagePackageWriteAppService>();

            var dto = await write.CreateAsync(new CreateUsagePackageInput
            {
                PartnerId = DeliveryPartnerId,
                Name = "Admin-managed delivery package",
                Mode = UsagePackageMode.Subscription,
                BaseSellAmount = 50m,
            });

            dto.PartnerId.ShouldBe(DeliveryPartnerId);
            dto.BaseSell!.Amount.ShouldBe(50m);
        });
    }

    [Fact]
    public async Task Cross_Partner_Self_Author_Is_Blocked()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            ConfigureSelfServiceAuthor(ServicePartnerId, PartnerType.Service);
            GetRequiredService<PartnerCatalogTestPartnerTypeLookup>().Set(DeliveryPartnerId, PartnerType.Service);

            var write = GetRequiredService<IUsagePackageWriteAppService>();
            await Should.ThrowAsync<AbpAuthorizationException>(() =>
                write.CreateAsync(new CreateUsagePackageInput
                {
                    PartnerId = DeliveryPartnerId,
                    Name = "Cross partner package",
                    Mode = UsagePackageMode.Resale,
                }));
        });
    }

    [Fact]
    public async Task Authoring_Does_Not_Post_Or_Create_Snapshot()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            ConfigureSelfServiceAuthor(ServicePartnerId, PartnerType.Service);
            var snapshotRepo = GetRequiredService<IRepository<SettlementCostMarkupSnapshot, Guid>>();
            var before = await snapshotRepo.GetCountAsync();

            var write = GetRequiredService<IUsagePackageWriteAppService>();
            var created = await write.CreateAsync(new CreateUsagePackageInput
            {
                PartnerId = ServicePartnerId,
                Name = "No posting package",
                Mode = UsagePackageMode.Resale,
                BaseBuyAmount = 10m,
                BaseSellAmount = 20m,
            });
            await write.PublishAsync(created.Id);
            await write.ArchiveAsync(created.Id);

            var after = await snapshotRepo.GetCountAsync();
            after.ShouldBe(before);
        });
    }

    private void ConfigureSelfServiceAuthor(Guid partnerId, PartnerType partnerType)
    {
        GetRequiredService<PartnerCatalogTestCurrentPartner>().Id = partnerId;
        GetRequiredService<PartnerCatalogTestPartnerTypeLookup>().Set(partnerId, partnerType);
        GetRequiredService<PartnerCatalogTestPermissionChecker>().GrantOnly(ZahyPermissions.Catalog.AuthorSelf);
    }

    private void ConfigureManagedAuthor(Guid partnerId, PartnerType partnerType)
    {
        GetRequiredService<PartnerCatalogTestCurrentPartner>().Id = null;
        GetRequiredService<PartnerCatalogTestPartnerTypeLookup>().Set(partnerId, partnerType);
        GetRequiredService<PartnerCatalogTestPermissionChecker>().GrantOnly(ZahyPermissions.Catalog.AuthorManaged);
    }
}
