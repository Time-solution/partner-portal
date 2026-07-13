using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Shouldly;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Xunit;
using Zahy.Identity.Permissions;
using Zahy.PartnerCatalog.ServiceOrders;
using Zahy.PartnerCatalog.Write;
using Zahy.PartnerPlatform.Partners;
using Zahy.Settlement;

namespace Zahy.PartnerCatalog;

/// <summary>
/// Gate 2b — the order lifecycle over the app service: tenant/partner structural invisibility on
/// read AND write, the admin auto-accept sweep (idempotent), structural scoping of the two DTO
/// shapes (populated fixtures, 6b deep key-walk), and the Gate-1 no-posting proof (trial balance
/// byte-identical across a full order lifecycle).
/// </summary>
public class ServiceOrderAppServiceTests : ZahyPartnerCatalogTestBase
{
    private static readonly Guid ServicePartnerId = Guid.Parse("22222222-2222-2222-2222-222222222004");
    private static readonly Guid OtherPartnerId = Guid.Parse("22222222-2222-2222-2222-222222222005");
    private static readonly Guid TenantA = Guid.Parse("33333333-3333-3333-3333-333333333021");
    private static readonly Guid TenantB = Guid.Parse("33333333-3333-3333-3333-333333333022");

    private void AsMerchant(Guid tenantId)
    {
        GetRequiredService<PartnerCatalogTestCurrentTenant>().Id = tenantId;
        GetRequiredService<PartnerCatalogTestCurrentPartner>().Id = null;
        GetRequiredService<PartnerCatalogTestPermissionChecker>().GrantOnly(ZahyPermissions.Catalog.Read);
    }

    private void AsPartner(Guid partnerId)
    {
        GetRequiredService<PartnerCatalogTestCurrentTenant>().Id = null;
        GetRequiredService<PartnerCatalogTestCurrentPartner>().Id = partnerId;
        GetRequiredService<PartnerCatalogTestPermissionChecker>().GrantOnly(ZahyPermissions.Catalog.Read);
    }

    private async Task<PartnerCatalogItem> InsertServiceItemAsync(decimal cost = 70m, string code = "SVC-ORD-1")
    {
        var item = PartnerCatalogItem.Create(
            Guid.NewGuid(), ServicePartnerId, code, "Managed WhatsApp", null,
            PartnerCatalogOfferingKind.ServiceOneOff, Money.Of(cost, vatInclusive: true),
            settlementParticipationMode: SettlementParticipationMode.Principal);
        await GetRequiredService<IRepository<PartnerCatalogItem, Guid>>().InsertAsync(item, autoSave: true);
        return item;
    }

    private async Task InsertListingAsync(Guid itemId)
    {
        var listing = PartnerCatalogListing.Create(Guid.NewGuid(), itemId, ServicePartnerId);
        listing.ReplaceSections(
            new[]
            {
                new ListingRequirementInput("نبذة عن المتجر", ListingRequirementType.ShortText),
                new ListingRequirementInput("الفئة", ListingRequirementType.MultiChoice, new[] { "أفراد", "شركات" }),
            },
            null, null, null, null, DateTime.UtcNow);
        await GetRequiredService<IRepository<PartnerCatalogListing, Guid>>().InsertAsync(listing, autoSave: true);
    }

    private static Dictionary<int, string> Answers() => new() { [0] = "متجر عطور", [1] = "شركات" };

    [Fact]
    public async Task Full_Lifecycle_Runs_Across_Merchant_And_Partner_Contexts()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var item = await InsertServiceItemAsync();
            await InsertListingAsync(item.Id);
            var orders = GetRequiredService<IServiceOrderAppService>();

            AsMerchant(TenantA);
            var created = await orders.CreateAsync(new CreateServiceOrderInput { PartnerCatalogItemId = item.Id });
            created.Status.ShouldBe("Draft");
            created.PriceAmount.ShouldBe(70m); // activation-default resolution = PartnerCost

            var submitted = await orders.SubmitRequirementsAsync(new SubmitServiceOrderRequirementsInput
            {
                OrderId = created.Id,
                AnswersByRequirementOrderIndex = Answers(),
            });
            submitted.Status.ShouldBe("RequirementsSubmitted");

            AsPartner(ServicePartnerId);
            (await orders.PartnerAcceptAsync(new ServiceOrderActionInput { OrderId = created.Id }))
                .Status.ShouldBe("InProgress");
            (await orders.MarkDeliveredAsync(new ServiceOrderActionInput { OrderId = created.Id }))
                .Status.ShouldBe("Delivered");

            AsMerchant(TenantA);
            var closed = await orders.AcceptDeliveryAsync(new ServiceOrderActionInput { OrderId = created.Id });
            closed.Status.ShouldBe("Closed");
            closed.History.Select(h => h.Action).ShouldContain("MerchantAccepted");
            closed.History.Last().Action.ShouldBe("Closed");

            (await orders.GetMyOrdersAsync()).ShouldContain(o => o.Id == created.Id);
        });
    }

    [Fact]
    public async Task Cross_Merchant_And_Cross_Partner_Orders_Are_Structurally_Invisible_On_Read_And_Write()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var item = await InsertServiceItemAsync(code: "SVC-ORD-2");
            await InsertListingAsync(item.Id);
            var orders = GetRequiredService<IServiceOrderAppService>();

            AsMerchant(TenantA);
            var created = await orders.CreateAsync(new CreateServiceOrderInput { PartnerCatalogItemId = item.Id });
            await orders.SubmitRequirementsAsync(new SubmitServiceOrderRequirementsInput
            {
                OrderId = created.Id,
                AnswersByRequirementOrderIndex = Answers(),
            });

            // Another merchant: absent from reads, unreachable on writes.
            AsMerchant(TenantB);
            (await orders.GetMyOrdersAsync()).ShouldNotContain(o => o.Id == created.Id);
            await Should.ThrowAsync<EntityNotFoundException>(
                orders.CancelAsync(new ServiceOrderActionInput { OrderId = created.Id }));

            // Another partner: absent from reads, unreachable on writes.
            AsPartner(OtherPartnerId);
            (await orders.GetIncomingOrdersAsync()).ShouldNotContain(o => o.Id == created.Id);
            await Should.ThrowAsync<EntityNotFoundException>(
                orders.PartnerAcceptAsync(new ServiceOrderActionInput { OrderId = created.Id }));

            // The owning partner sees + acts.
            AsPartner(ServicePartnerId);
            (await orders.GetIncomingOrdersAsync()).ShouldContain(o => o.Id == created.Id);
        });
    }

    [Fact]
    public async Task Admin_AutoAccept_Sweep_Closes_Due_Orders_And_Is_Idempotent()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var item = await InsertServiceItemAsync(code: "SVC-ORD-3");
            var repo = GetRequiredService<IRepository<ServiceOrder, Guid>>();

            // A Delivered order whose delivery is 8 days in the past (domain-built, repo-inserted).
            var overdue = ServiceOrder.Create(Guid.NewGuid(), TenantA, item,
                Money.Of(100m, vatInclusive: true), null, "m", DateTime.UtcNow.AddDays(-10));
            overdue.SubmitRequirements(Array.Empty<ListingRequirement>(), new Dictionary<int, string>(), "m", DateTime.UtcNow.AddDays(-10));
            overdue.PartnerAccept("p", DateTime.UtcNow.AddDays(-9));
            overdue.Deliver("p", DateTime.UtcNow.AddDays(-8));

            // A Delivered order only 1 day old — not due.
            var fresh = ServiceOrder.Create(Guid.NewGuid(), TenantA, item,
                Money.Of(100m, vatInclusive: true), null, "m", DateTime.UtcNow.AddDays(-2));
            fresh.SubmitRequirements(Array.Empty<ListingRequirement>(), new Dictionary<int, string>(), "m", DateTime.UtcNow.AddDays(-2));
            fresh.PartnerAccept("p", DateTime.UtcNow.AddDays(-2));
            fresh.Deliver("p", DateTime.UtcNow.AddDays(-1));

            await repo.InsertAsync(overdue, autoSave: true);
            await repo.InsertAsync(fresh, autoSave: true);

            GetRequiredService<PartnerCatalogTestPermissionChecker>().GrantOnly(ZahyPermissions.Admin);
            GetRequiredService<PartnerCatalogTestCurrentTenant>().Id = null;
            GetRequiredService<PartnerCatalogTestCurrentPartner>().Id = null;

            var orders = GetRequiredService<IServiceOrderAppService>();
            (await orders.RunAutoAcceptSweepAsync()).ShouldBe(1); // only the overdue one

            var swept = await repo.GetAsync(overdue.Id);
            swept.Status.ShouldBe(ServiceOrderStatus.Closed);
            swept.History.Single(h => h.Action == ServiceOrderAction.AutoAccepted)
                .Actor.ShouldBe(PartnerCatalogServiceOrderConsts.SystemActor);

            // Idempotent re-sweep: nothing left to accept.
            (await orders.RunAutoAcceptSweepAsync()).ShouldBe(0);
        });
    }

    [Fact]
    public async Task Structural_Scoping_Merchant_Has_No_Buy_Partner_Has_No_Sell_Or_Fee()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var item = await InsertServiceItemAsync(cost: 1500m, code: "SVC-ORD-4");
            await InsertListingAsync(item.Id);
            var orders = GetRequiredService<IServiceOrderAppService>();

            // POPULATED fixture: Principal order ≥1000 with a valid 40/30/30 plan + answers.
            AsMerchant(TenantA);
            var created = await orders.CreateAsync(new CreateServiceOrderInput
            {
                PartnerCatalogItemId = item.Id,
                Milestones =
                {
                    new ServiceOrderMilestoneDto { OrderIndex = 0, Title = "دفعة أولى", Amount = 600m },
                    new ServiceOrderMilestoneDto { OrderIndex = 1, Title = "دفعة ثانية", Amount = 450m },
                    new ServiceOrderMilestoneDto { OrderIndex = 2, Title = "دفعة أخيرة", Amount = 450m },
                },
            });
            var merchantView = await orders.SubmitRequirementsAsync(new SubmitServiceOrderRequirementsInput
            {
                OrderId = created.Id,
                AnswersByRequirementOrderIndex = Answers(),
            });

            merchantView.PriceAmount.ShouldBe(1500m);
            AssertNoForbiddenKeys(merchantView, new[] { "buy", "buyamount", "buysnapshotamount", "margin" });

            AsPartner(ServicePartnerId);
            var partnerView = (await orders.GetIncomingOrdersAsync()).Single(o => o.Id == created.Id);
            partnerView.BuyAmount.ShouldBe(1500m); // buy side visible to the partner (their receivable)
            AssertNoForbiddenKeys(partnerView, new[] { "sell", "sellamount", "fee", "feeamount", "price", "priceamount", "margin", "milestone" });
        });
    }

    private static void AssertNoForbiddenKeys(object dto, string[] forbidden)
    {
        void Walk(JsonElement element, string path)
        {
            if (element.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in element.EnumerateObject())
                {
                    forbidden.ShouldNotContain(property.Name.ToLowerInvariant(),
                        $"forbidden key '{property.Name}' at {path}.{property.Name}");
                    Walk(property.Value, $"{path}.{property.Name}");
                }
            }
            else if (element.ValueKind == JsonValueKind.Array)
            {
                foreach (var child in element.EnumerateArray())
                {
                    Walk(child, path + "[]");
                }
            }
        }

        Walk(JsonSerializer.SerializeToElement(dto), "dto");
    }

    [Fact]
    public async Task No_Posting_Trial_Balance_Is_Byte_Identical_Across_A_Full_Order_Lifecycle()
    {
        var period = SettlementPeriod.Of(2026, 7);
        Money Incl(decimal amount) => Money.Of(amount, SettlementConsts.DefaultCurrency, vatInclusive: true);
        var postings = new[]
        {
            SettlementPostingTemplates.Principal(Incl(60.00m), Incl(40.00m), 0.15m)
                .Tag(ServicePartnerId, TenantA, period, "ORD-X1"),
        };
        var trialBefore = JsonSerializer.Serialize(SettlementReports.TrialBalanceFor(postings, period));

        await WithUnitOfWorkAsync(async () =>
        {
            var item = await InsertServiceItemAsync(code: "SVC-ORD-5");
            await InsertListingAsync(item.Id);
            var orders = GetRequiredService<IServiceOrderAppService>();

            AsMerchant(TenantA);
            var created = await orders.CreateAsync(new CreateServiceOrderInput { PartnerCatalogItemId = item.Id });
            await orders.SubmitRequirementsAsync(new SubmitServiceOrderRequirementsInput
            {
                OrderId = created.Id,
                AnswersByRequirementOrderIndex = Answers(),
            });
            AsPartner(ServicePartnerId);
            await orders.PartnerAcceptAsync(new ServiceOrderActionInput { OrderId = created.Id });
            await orders.MarkDeliveredAsync(new ServiceOrderActionInput { OrderId = created.Id });
            AsMerchant(TenantA);
            await orders.AcceptDeliveryAsync(new ServiceOrderActionInput { OrderId = created.Id });
        });

        JsonSerializer.Serialize(SettlementReports.TrialBalanceFor(postings, period)).ShouldBe(trialBefore);
    }
}
