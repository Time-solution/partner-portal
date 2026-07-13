using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Authorization;
using Xunit;
using Zahy.Identity.Permissions;
using Zahy.PartnerCatalog.Write;
using Zahy.PartnerPlatform.Partners;

namespace Zahy.PartnerCatalog;

/// <summary>
/// Gate 2a — the listing WRITE path through the app service, reusing the existing authoring gate
/// (PartnerCatalogWriteAccessGuard): a service partner self-authors the new sections on its OWN
/// offering; a foreign partner is blocked; a delivery/3PL partner is admin-managed (blocked on
/// self-author); admin authors any. Also proves empty-section-clears on the real write path.
/// </summary>
public class PartnerCatalogListingAppServiceTests : ZahyPartnerCatalogTestBase
{
    private static readonly Guid ServicePartnerId = Guid.Parse("22222222-2222-2222-2222-222222222004");
    private static readonly Guid OtherPartnerId = Guid.Parse("22222222-2222-2222-2222-222222222005");
    private static readonly Guid DeliveryPartnerId = Guid.Parse("22222222-2222-2222-2222-222222222001");

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

    private async Task<Guid> CreateServiceOfferingAsync(string code)
    {
        var write = GetRequiredService<IPartnerCatalogWriteAppService>();
        var dto = await write.CreateAsync(new CreatePartnerCatalogItemInput
        {
            PartnerId = ServicePartnerId,
            Code = code,
            Name = "Managed WhatsApp",
            OfferingKind = PartnerCatalogOfferingKind.ServiceOneOff,
            PartnerCost = new Read.MoneyDto { Amount = 70m, Currency = "SAR", VatInclusive = true },
        });
        return dto.Id;
    }

    private static UpdatePartnerCatalogListingInput SampleInput() => new()
    {
        Requirements =
        {
            new ListingRequirementDto { OrderIndex = 0, Title = "شعار المتجر", Type = ListingRequirementType.FileUpload },
        },
        Deliverables =
        {
            new ListingDeliverableDto { OrderIndex = 0, Title = "تقرير أداء", Quantity = 2 },
        },
        ExecutionSteps = { new ListingTextRowDto { OrderIndex = 0, Text = "استلام المتطلبات" } },
        Terms = { new ListingTextRowDto { OrderIndex = 0, Text = "جولتا مراجعة" } },
        Faqs = { new ListingFaqDto { OrderIndex = 0, Question = "متى؟", Answer = "خلال خمسة أيام." } },
    };

    [Fact]
    public async Task Service_Partner_Self_Authors_Listing_On_Own_Offering()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            AsSelfPartner(ServicePartnerId, PartnerType.Service);
            var itemId = await CreateServiceOfferingAsync("SVC-LST-1");

            var listings = GetRequiredService<IPartnerCatalogListingAppService>();
            var saved = await listings.UpdateListingAsync(itemId, SampleInput());

            saved.Requirements.Single().Title.ShouldBe("شعار المتجر");
            saved.Deliverables.Single().Quantity.ShouldBe(2);
            (await listings.GetListingAsync(itemId)).Faqs.Single().Answer.ShouldBe("خلال خمسة أيام.");
        });
    }

    [Fact]
    public async Task Cross_Partner_Listing_Write_Is_Blocked()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            AsSelfPartner(ServicePartnerId, PartnerType.Service);
            var itemId = await CreateServiceOfferingAsync("SVC-LST-2");

            // A DIFFERENT partner (also service, AuthorSelf) targets the first partner's offering.
            // The partner data filter makes the foreign item STRUCTURALLY INVISIBLE — the write
            // path cannot even resolve it (EntityNotFound), which is stronger than a 403 and
            // matches the established read-path convention.
            AsSelfPartner(OtherPartnerId, PartnerType.Service);
            GetRequiredService<PartnerCatalogTestPartnerTypeLookup>().Set(ServicePartnerId, PartnerType.Service);

            var listings = GetRequiredService<IPartnerCatalogListingAppService>();
            await Should.ThrowAsync<Volo.Abp.Domain.Entities.EntityNotFoundException>(
                listings.UpdateListingAsync(itemId, SampleInput()));
        });
    }

    [Fact]
    public async Task Delivery_3PL_Partner_Cannot_Self_Author_Listing_But_Admin_Can()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            // Admin creates the admin-managed delivery offering.
            AsAdmin(DeliveryPartnerId, PartnerType.ThreePL);
            var write = GetRequiredService<IPartnerCatalogWriteAppService>();
            var item = await write.CreateAsync(new CreatePartnerCatalogItemInput
            {
                PartnerId = DeliveryPartnerId,
                Code = "DLV-LST-1",
                Name = "Fulfilment",
                OfferingKind = PartnerCatalogOfferingKind.DeliveryFulfilmentPerOrder,
                PartnerCost = new Read.MoneyDto { Amount = 10m, Currency = "SAR", VatInclusive = true },
            });

            var listings = GetRequiredService<IPartnerCatalogListingAppService>();

            // The 3PL partner itself (AuthorSelf) is admin-managed ⇒ blocked on the NEW sections.
            AsSelfPartner(DeliveryPartnerId, PartnerType.ThreePL);
            await Should.ThrowAsync<BusinessException>(
                listings.UpdateListingAsync(item.Id, SampleInput()));

            // Admin authors any.
            AsAdmin(DeliveryPartnerId, PartnerType.ThreePL);
            var saved = await listings.UpdateListingAsync(item.Id, SampleInput());
            saved.ExecutionSteps.Single().Text.ShouldBe("استلام المتطلبات");
        });
    }

    [Fact]
    public async Task Empty_Input_Clears_The_Listing_On_The_Write_Path()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            AsSelfPartner(ServicePartnerId, PartnerType.Service);
            var itemId = await CreateServiceOfferingAsync("SVC-LST-3");

            var listings = GetRequiredService<IPartnerCatalogListingAppService>();
            await listings.UpdateListingAsync(itemId, SampleInput());
            (await listings.GetListingAsync(itemId)).Requirements.ShouldNotBeEmpty();

            // Whole-document replace with every section empty ⇒ CLEARS (write path, not render).
            var cleared = await listings.UpdateListingAsync(itemId, new UpdatePartnerCatalogListingInput());
            cleared.Requirements.ShouldBeEmpty();
            cleared.Deliverables.ShouldBeEmpty();
            cleared.ExecutionSteps.ShouldBeEmpty();
            cleared.Terms.ShouldBeEmpty();
            cleared.Faqs.ShouldBeEmpty();

            (await listings.GetListingAsync(itemId)).Faqs.ShouldBeEmpty();
        });
    }
}
