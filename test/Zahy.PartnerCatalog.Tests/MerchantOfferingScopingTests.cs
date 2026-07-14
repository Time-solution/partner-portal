using System;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Xunit;
using Zahy.PartnerCatalog.Merchant;
using Zahy.PartnerCatalog.Read;
using Zahy.Settlement;

namespace Zahy.PartnerCatalog;

/// <summary>
/// CAT-FIX 3 — offering DTO audience scoping: the merchant browse projection is built by ONE audited
/// mapper (<see cref="PartnerCatalogOfferingVisibility"/>), the merchant sees a resolved SELL named
/// Price, and the BUY leg (PartnerCost) is STRUCTURALLY ABSENT from the merchant DTO type. The
/// partner/admin read (central <see cref="PartnerCatalogReadDtoMapper"/>) keeps PartnerCost — unchanged.
/// </summary>
public class MerchantOfferingScopingTests : ZahyPartnerCatalogTestBase
{
    private static readonly Guid PartnerId = Guid.Parse("22222222-2222-2222-2222-222222222001");
    private static readonly Guid TenantA = Guid.Parse("11111111-1111-1111-1111-111111111001");

    [Fact]
    public void Merchant_Offering_Dto_Has_No_Buy_Leg_Structurally()
    {
        var properties = typeof(MerchantPartnerOfferingReadDto).GetProperties();

        // Absent means the property does not exist on the type — not hidden, not null.
        properties.ShouldNotContain(p => p.Name == "PartnerCost");
        properties.ShouldNotContain(p => p.Name.Contains("Buy", StringComparison.OrdinalIgnoreCase));
        properties.ShouldNotContain(p => p.Name.Contains("Cost", StringComparison.OrdinalIgnoreCase));
        properties.ShouldNotContain(p => p.Name.Contains("Margin", StringComparison.OrdinalIgnoreCase));

        properties.ShouldContain(p => p.Name == "Price");
    }

    [Fact]
    public void Partner_And_Admin_Item_Read_Keeps_The_Buy_Leg_Unchanged()
    {
        typeof(PartnerCatalogItemReadDto).GetProperty("PartnerCost").ShouldNotBeNull();
    }

    [Fact]
    public async Task Browse_Price_Is_Exactly_What_Activation_Charges_By_Default()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var item = await InsertActiveItemAsync("SCOPE-PRICE-1", cost: 40m);
            SetTenant(TenantA);

            var merchant = GetRequiredService<IPartnerCatalogMerchantAppService>();

            var offering = (await merchant.GetAvailableOfferingsAsync()).Single(o => o.Code == "SCOPE-PRICE-1");
            offering.Price.Amount.ShouldBe(40m);
            offering.Price.VatInclusive.ShouldBeTrue();

            // The default activation charges the same resolved sell the browse displayed.
            var activation = await merchant.ActivateAsync(new ActivateMerchantOfferingInput
            {
                PartnerCatalogItemId = item.Id,
            });
            activation.ResalePrice.Amount.ShouldBe(offering.Price.Amount);
        });
    }

    [Fact]
    public async Task Browse_Price_Reflects_The_Open_Activation_And_Falls_Back_After_End()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var item = await InsertActiveItemAsync("SCOPE-PRICE-2", cost: 40m);
            SetTenant(TenantA);

            var merchant = GetRequiredService<IPartnerCatalogMerchantAppService>();
            var activation = await merchant.ActivateAsync(new ActivateMerchantOfferingInput
            {
                PartnerCatalogItemId = item.Id,
                ResalePrice = new MoneyDto { Amount = 85m, Currency = "SAR", VatInclusive = true },
            });

            var whileOpen = (await merchant.GetAvailableOfferingsAsync()).Single(o => o.Code == "SCOPE-PRICE-2");
            whileOpen.Price.Amount.ShouldBe(85m);

            // Ended cycles never drive pricing — browse falls back to the resolved default.
            await merchant.DeactivateAsync(activation.Id);
            var afterEnd = (await merchant.GetAvailableOfferingsAsync()).Single(o => o.Code == "SCOPE-PRICE-2");
            afterEnd.Price.Amount.ShouldBe(40m);
        });
    }

    private void SetTenant(Guid tenantId) =>
        GetRequiredService<PartnerCatalogTestCurrentTenant>().Id = tenantId;

    private async Task<PartnerCatalogItem> InsertActiveItemAsync(string code, decimal cost)
    {
        var itemRepo = GetRequiredService<IRepository<PartnerCatalogItem, Guid>>();
        var item = PartnerCatalogItem.Create(
            Guid.NewGuid(),
            PartnerId,
            code,
            code,
            null,
            PartnerCatalogOfferingKind.ServiceOneOff,
            Money.Of(cost, vatInclusive: true));
        item.Publish(DateTime.UtcNow);
        await itemRepo.InsertAsync(item, autoSave: true);
        return item;
    }
}
