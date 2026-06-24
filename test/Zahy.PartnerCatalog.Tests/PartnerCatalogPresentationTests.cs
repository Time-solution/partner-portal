using System;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Authorization;
using Xunit;
using Zahy.Identity.Permissions;
using Zahy.PartnerCatalog.Merchant;
using Zahy.PartnerCatalog.Packages;
using Zahy.PartnerCatalog.Profile;
using Zahy.PartnerCatalog.Read;
using Zahy.PartnerCatalog.Write;
using Zahy.PartnerPlatform.Partners;
using Zahy.Settlement;

namespace Zahy.PartnerCatalog;

/// <summary>
/// Phase 6a — partner authoring presentation (PartnerBrief / MerchantBenefit / OfferingSummary /
/// PackageExplanation) + merchant read-side visibility. Verifies the authoring-by-type rule is reused
/// (service self-authors offering/package presentation; delivery/3PL is admin-managed; the brief is
/// editable by ALL types on the partner's own profile), that the merchant browse + package sell-side
/// views carry the fields read-only, that cost/margin remain structurally absent in merchant scope, and
/// that the plain-text length caps / null-allowed rules hold.
/// </summary>
public class PartnerCatalogPresentationTests : ZahyPartnerCatalogTestBase
{
    private static readonly Guid ServicePartnerId = Guid.Parse("22222222-2222-2222-2222-222222222004");
    private static readonly Guid DeliveryPartnerId = Guid.Parse("22222222-2222-2222-2222-222222222001");
    private static readonly Guid MerchantTenantId = Guid.Parse("33333333-3333-3333-3333-333333333010");

    // ---- authoring context helpers (mirror the existing write-service tests) ----

    private void AsSelfPartner(Guid partnerId, PartnerType partnerType)
    {
        GetRequiredService<PartnerCatalogTestCurrentPartner>().Id = partnerId;
        GetRequiredService<PartnerCatalogTestPartnerTypeLookup>().Set(partnerId, partnerType);
        GetRequiredService<PartnerCatalogTestPermissionChecker>().GrantOnly(ZahyPermissions.Catalog.AuthorSelf);
    }

    private void AsAdmin(Guid partnerId, PartnerType partnerType)
    {
        GetRequiredService<PartnerCatalogTestCurrentPartner>().Id = null;
        GetRequiredService<PartnerCatalogTestPartnerTypeLookup>().Set(partnerId, partnerType);
        GetRequiredService<PartnerCatalogTestPermissionChecker>().GrantOnly(ZahyPermissions.Catalog.AuthorManaged);
    }

    private void AsMerchant(Guid tenantId)
    {
        GetRequiredService<PartnerCatalogTestCurrentPartner>().Id = null;
        GetRequiredService<PartnerCatalogTestCurrentTenant>().Id = tenantId;
        GetRequiredService<PartnerCatalogTestPermissionChecker>().GrantOnly(string.Empty);
    }

    // ---- PartnerBrief (partner-level, ALL types) ----

    [Fact]
    public async Task Service_Partner_Sets_And_Edits_Own_Brief()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            AsSelfPartner(ServicePartnerId, PartnerType.Service);
            var profiles = GetRequiredService<IPartnerCatalogProfileAppService>();

            var set = await profiles.SetBriefAsync(new SetPartnerBriefInput
            {
                PartnerId = ServicePartnerId,
                PartnerBrief = "نقدّم حلول مطاعم متكاملة.",
            });
            set.PartnerBrief.ShouldBe("نقدّم حلول مطاعم متكاملة.");

            var edited = await profiles.SetBriefAsync(new SetPartnerBriefInput
            {
                PartnerId = ServicePartnerId,
                PartnerBrief = "تحديث: حلول مطاعم + توصيل.",
            });
            edited.PartnerBrief.ShouldBe("تحديث: حلول مطاعم + توصيل.");
            edited.Id.ShouldBe(set.Id); // same profile row, edited in place

            (await profiles.GetAsync(ServicePartnerId))!.PartnerBrief.ShouldBe("تحديث: حلول مطاعم + توصيل.");
        });
    }

    [Fact]
    public async Task Delivery_Type_Partner_Can_Still_Set_Own_Brief()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            // The brief is self-description, NOT pricing — editable by ALL partner types (here: Carrier).
            AsSelfPartner(DeliveryPartnerId, PartnerType.Carrier);
            var profiles = GetRequiredService<IPartnerCatalogProfileAppService>();

            var set = await profiles.SetBriefAsync(new SetPartnerBriefInput
            {
                PartnerId = DeliveryPartnerId,
                PartnerBrief = "شركة شحن وتوصيل.",
            });

            set.PartnerBrief.ShouldBe("شركة شحن وتوصيل.");
        });
    }

    [Fact]
    public async Task Cross_Partner_Brief_Edit_Is_Blocked()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            AsSelfPartner(ServicePartnerId, PartnerType.Service);
            var profiles = GetRequiredService<IPartnerCatalogProfileAppService>();

            await Should.ThrowAsync<AbpAuthorizationException>(() =>
                profiles.SetBriefAsync(new SetPartnerBriefInput
                {
                    PartnerId = DeliveryPartnerId, // not my partner
                    PartnerBrief = "محاولة تعديل بريف شريك آخر.",
                }));
        });
    }

    // ---- MerchantBenefit (offering-level) + PackageExplanation (package-level): authoring-by-type ----

    [Fact]
    public async Task Service_Partner_Self_Authors_Benefit_And_Explanation()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            AsSelfPartner(ServicePartnerId, PartnerType.Service);

            var write = GetRequiredService<IPartnerCatalogWriteAppService>();
            var item = await write.CreateAsync(new CreatePartnerCatalogItemInput
            {
                PartnerId = ServicePartnerId,
                Code = "SVC-PRESENT",
                Name = "Loyalty service",
                Description = "Drives repeat orders.",
                MerchantBenefit = "زيادة الطلبات المتكررة حتى ٢٠٪.",
                OfferingKind = PartnerCatalogOfferingKind.ServiceSubscription,
                PartnerCost = new MoneyDto { Amount = 99m, Currency = "SAR", VatInclusive = true },
            });
            item.MerchantBenefit.ShouldBe("زيادة الطلبات المتكررة حتى ٢٠٪.");
            item.Description.ShouldBe("Drives repeat orders.");

            var packages = GetRequiredService<IUsagePackageWriteAppService>();
            var pkg = await packages.CreateAsync(new CreateUsagePackageInput
            {
                PartnerId = ServicePartnerId,
                Name = "Messages 10k",
                UnitLabel = "messages",
                PackageExplanation = "يشمل ١٠٬٠٠٠ رسالة شهريًا ثم تُحتسب الزيادة بالسعر المبيّن.",
                Mode = UsagePackageMode.Subscription,
                IncludedQuantity = 10000m,
                BaseSellAmount = 149m,
                OverageSellAmount = 0.05m,
                Payer = ActivationFeePayer.Merchant,
            });
            pkg.PackageExplanation.ShouldBe("يشمل ١٠٬٠٠٠ رسالة شهريًا ثم تُحتسب الزيادة بالسعر المبيّن.");
        });
    }

    [Fact]
    public async Task Delivery_Partner_Blocked_From_Self_Authoring_Offering_Benefit()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            AsSelfPartner(DeliveryPartnerId, PartnerType.Carrier);

            var write = GetRequiredService<IPartnerCatalogWriteAppService>();
            var ex = await Should.ThrowAsync<BusinessException>(() =>
                write.CreateAsync(new CreatePartnerCatalogItemInput
                {
                    PartnerId = DeliveryPartnerId,
                    Code = "DEL-PRESENT",
                    Name = "Delivery with benefit",
                    MerchantBenefit = "توصيل أسرع.",
                    OfferingKind = PartnerCatalogOfferingKind.DeliveryFulfilmentPerOrder,
                    PartnerCost = new MoneyDto { Amount = 10m, Currency = "SAR", VatInclusive = true },
                    FulfilmentUnit = PartnerCatalogFulfilmentUnit.PerShipment,
                }));

            ex.Code.ShouldBe(PartnerCatalogErrorCodes.AuthoringNotPermitted);
        });
    }

    [Fact]
    public async Task Delivery_Partner_Blocked_From_Self_Authoring_Package_Explanation()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            AsSelfPartner(DeliveryPartnerId, PartnerType.Carrier);

            var packages = GetRequiredService<IUsagePackageWriteAppService>();
            var ex = await Should.ThrowAsync<BusinessException>(() =>
                packages.CreateAsync(new CreateUsagePackageInput
                {
                    PartnerId = DeliveryPartnerId,
                    Name = "Carrier package",
                    PackageExplanation = "شرح غير مسموح للناقل.",
                    Mode = UsagePackageMode.Resale,
                    BaseSellAmount = 50m,
                }));

            ex.Code.ShouldBe(PartnerCatalogErrorCodes.AuthoringNotPermitted);
        });
    }

    // ---- Merchant read-side visibility + cost/margin regression ----

    [Fact]
    public async Task Merchant_Browse_Carries_Brief_Benefit_And_Summary()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            // Admin authors: partner brief + an active offering with benefit + a summary (Description).
            AsAdmin(ServicePartnerId, PartnerType.Service);
            await GetRequiredService<IPartnerCatalogProfileAppService>().SetBriefAsync(new SetPartnerBriefInput
            {
                PartnerId = ServicePartnerId,
                PartnerBrief = "مزود خدمات ولاء.",
            });

            var write = GetRequiredService<IPartnerCatalogWriteAppService>();
            var created = await write.CreateAsync(new CreatePartnerCatalogItemInput
            {
                PartnerId = ServicePartnerId,
                Code = "SVC-BROWSE",
                Name = "Loyalty",
                Description = "Repeat-order engine.",
                MerchantBenefit = "ولاء العملاء.",
                OfferingKind = PartnerCatalogOfferingKind.ServiceOneOff,
                PartnerCost = new MoneyDto { Amount = 40m, Currency = "SAR", VatInclusive = true },
            });
            await write.PublishAsync(created.Id);

            AsMerchant(MerchantTenantId);
            var merchant = GetRequiredService<IPartnerCatalogMerchantAppService>();
            var offering = (await merchant.GetAvailableOfferingsAsync()).Single(o => o.Code == "SVC-BROWSE");

            offering.PartnerBrief.ShouldBe("مزود خدمات ولاء.");
            offering.MerchantBenefit.ShouldBe("ولاء العملاء.");
            offering.Description.ShouldBe("Repeat-order engine."); // OfferingSummary == Description (reused)
        });
    }

    [Fact]
    public async Task Merchant_Package_View_Carries_Explanation_And_Hides_Cost_And_Margin()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            AsAdmin(ServicePartnerId, PartnerType.Service);
            var packages = GetRequiredService<IUsagePackageWriteAppService>();
            var pkg = await packages.CreateAsync(new CreateUsagePackageInput
            {
                PartnerId = ServicePartnerId,
                Name = "Resale 10k",
                UnitLabel = "messages",
                PackageExplanation = "باقة ١٠ آلاف رسالة شهريًا.",
                Mode = UsagePackageMode.Resale,
                IncludedQuantity = 10000m,
                BaseBuyAmount = 200m,
                BaseSellAmount = 300m,
                OverageBuyAmount = 0.02m,
                OverageSellAmount = 0.05m,
            });
            await packages.PublishAsync(pkg.Id);

            AsMerchant(MerchantTenantId);
            var svc = GetRequiredService<IUsagePackageSelectionAppService>();
            var view = (await svc.GetPublishedAsync(ServicePartnerId)).Single();

            // Presentation + structured details present on the sell-side view.
            view.Audience.ShouldBe(UsagePackageAudience.Merchant);
            view.PackageExplanation.ShouldBe("باقة ١٠ آلاف رسالة شهريًا.");
            view.IncludedQuantity.ShouldBe(10000m);
            view.BaseSell!.Amount.ShouldBe(300m);
            view.OverageSell!.Amount.ShouldBe(0.05m);

            // REGRESSION: cost / margin / buy-price are STRUCTURALLY ABSENT in merchant scope.
            view.BaseBuy.ShouldBeNull();
            view.OverageBuy.ShouldBeNull();
            view.BaseMargin.ShouldBeNull();
            view.OverageMargin.ShouldBeNull();
        });
    }

    // ---- validation: caps, null allowed, plain text (no HTML passthrough) ----

    [Fact]
    public async Task Presentation_Fields_Enforce_Caps_Allow_Null_And_Store_Plain_Text()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            AsSelfPartner(ServicePartnerId, PartnerType.Service);
            var profiles = GetRequiredService<IPartnerCatalogProfileAppService>();

            // Length cap on the brief (>600).
            var tooLongBrief = new string('ب', PartnerCatalogConsts.MaxPartnerBriefLength + 1);
            var briefEx = await Should.ThrowAsync<BusinessException>(() =>
                profiles.SetBriefAsync(new SetPartnerBriefInput { PartnerId = ServicePartnerId, PartnerBrief = tooLongBrief }));
            briefEx.Code.ShouldBe(PartnerCatalogErrorCodes.InvalidPartnerCatalogProfile);

            // Null is allowed (clears the brief).
            var cleared = await profiles.SetBriefAsync(new SetPartnerBriefInput { PartnerId = ServicePartnerId, PartnerBrief = null });
            cleared.PartnerBrief.ShouldBeNull();

            // Plain text: angle-bracket content is stored verbatim (inert text, not stripped/executed).
            const string raw = "<b>عرض</b> <script>alert(1)</script>";
            var stored = await profiles.SetBriefAsync(new SetPartnerBriefInput { PartnerId = ServicePartnerId, PartnerBrief = raw });
            stored.PartnerBrief.ShouldBe(raw);

            // Offering benefit cap (>400) — domain owns the invariant (no DTO data-annotation preempts it).
            var write = GetRequiredService<IPartnerCatalogWriteAppService>();
            var benefitEx = await Should.ThrowAsync<BusinessException>(() =>
                write.CreateAsync(new CreatePartnerCatalogItemInput
                {
                    PartnerId = ServicePartnerId,
                    Code = "SVC-CAP",
                    Name = "Cap test",
                    MerchantBenefit = new string('x', PartnerCatalogConsts.MaxMerchantBenefitLength + 1),
                    OfferingKind = PartnerCatalogOfferingKind.ServiceOneOff,
                    PartnerCost = new MoneyDto { Amount = 1m, Currency = "SAR", VatInclusive = true },
                }));
            benefitEx.Code.ShouldBe(PartnerCatalogErrorCodes.InvalidOfferingKind);

            // Package explanation cap (>500).
            var packages = GetRequiredService<IUsagePackageWriteAppService>();
            var explEx = await Should.ThrowAsync<BusinessException>(() =>
                packages.CreateAsync(new CreateUsagePackageInput
                {
                    PartnerId = ServicePartnerId,
                    Name = "Cap package",
                    PackageExplanation = new string('y', PartnerCatalogConsts.MaxPackageExplanationLength + 1),
                    Mode = UsagePackageMode.Resale,
                    BaseSellAmount = 10m,
                }));
            explEx.Code.ShouldBe(PartnerCatalogErrorCodes.InvalidUsagePackage);
        });
    }
}
