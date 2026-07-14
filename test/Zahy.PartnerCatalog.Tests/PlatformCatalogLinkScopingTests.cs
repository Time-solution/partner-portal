using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Shouldly;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Xunit;
using Zahy.PartnerCatalog.Merchant;
using Zahy.Settlement;

namespace Zahy.PartnerCatalog;

/// <summary>
/// P12 — PlatformCatalogLink under the SAME partner-scoping standard as the other ten chain
/// entities (eleven now): PartnerId denormalized at creation, partner query filter on reads,
/// platform/null-partner context short-circuits (integration + admin unchanged), and the merchant
/// audience never sees the type at all (structural guard). Pattern B stays DeferredShape2 — no sync.
/// </summary>
public class PlatformCatalogLinkScopingTests : ZahyPartnerCatalogTestBase
{
    private static readonly Guid PartnerA = Guid.Parse("22222222-2222-2222-2222-222222222001");
    private static readonly Guid PartnerB = Guid.Parse("33333333-3333-3333-3333-333333333003");

    private static PartnerCatalogItem FnBItem(Guid partnerId, string code)
    {
        var item = PartnerCatalogItem.Create(
            Guid.NewGuid(),
            partnerId,
            code,
            code,
            null,
            PartnerCatalogOfferingKind.FnBItemsPerSale,
            Money.Of(25m, vatInclusive: true),
            externalMenuItemId: $"MENU-{code}");
        item.Publish(DateTime.UtcNow);
        return item;
    }

    [Fact]
    public void Link_Creation_Stamps_PartnerId_From_The_Source_Item()
    {
        var item = FnBItem(PartnerB, "STAMP-1");
        var link = PlatformCatalogLink.CreateDeferredShape2(Guid.NewGuid(), item);

        link.PartnerId.ShouldBe(PartnerB);
        link.Status.ShouldBe(PlatformCatalogLinkStatus.DeferredShape2); // Pattern B stays unwired
    }

    [Fact]
    public async Task Partner_Filter_Hides_Other_Partners_Links()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var itemRepo = GetRequiredService<IRepository<PartnerCatalogItem, Guid>>();
            var linkRepo = GetRequiredService<IRepository<PlatformCatalogLink, Guid>>();

            var itemA = FnBItem(PartnerA, "LNK-A");
            var itemB = FnBItem(PartnerB, "LNK-B");
            await itemRepo.InsertAsync(itemA, autoSave: true);
            await itemRepo.InsertAsync(itemB, autoSave: true);

            await linkRepo.InsertAsync(PlatformCatalogLink.CreateDeferredShape2(Guid.NewGuid(), itemA), autoSave: true);
            await linkRepo.InsertAsync(PlatformCatalogLink.CreateDeferredShape2(Guid.NewGuid(), itemB), autoSave: true);

            var dataFilter = GetRequiredService<IDataFilter>();
            var currentPartner = GetRequiredService<PartnerCatalogTestCurrentPartner>();

            // Partner A context — sees ONLY its own link (partner B's is structurally invisible).
            currentPartner.Id = PartnerA;
            using (dataFilter.Enable<IPartnerCatalogDataFilter>())
            {
                var visible = await linkRepo.GetListAsync();
                visible.Count.ShouldBe(1);
                visible.Single().PartnerId.ShouldBe(PartnerA);
            }
        });
    }

    [Fact]
    public async Task Platform_Null_Partner_Context_Reads_All_Links()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var itemRepo = GetRequiredService<IRepository<PartnerCatalogItem, Guid>>();
            var linkRepo = GetRequiredService<IRepository<PlatformCatalogLink, Guid>>();

            var itemA = FnBItem(PartnerA, "ALL-A");
            var itemB = FnBItem(PartnerB, "ALL-B");
            await itemRepo.InsertAsync(itemA, autoSave: true);
            await itemRepo.InsertAsync(itemB, autoSave: true);

            await linkRepo.InsertAsync(PlatformCatalogLink.CreateDeferredShape2(Guid.NewGuid(), itemA), autoSave: true);
            await linkRepo.InsertAsync(PlatformCatalogLink.CreateDeferredShape2(Guid.NewGuid(), itemB), autoSave: true);

            var dataFilter = GetRequiredService<IDataFilter>();
            var currentPartner = GetRequiredService<PartnerCatalogTestCurrentPartner>();

            // Platform/admin context (no partner id) — the filter short-circuits even when ENABLED,
            // so integration and admin reads are unchanged by construction.
            currentPartner.Id = null;
            using (dataFilter.Enable<IPartnerCatalogDataFilter>())
            {
                (await linkRepo.GetCountAsync()).ShouldBe(2);
            }
        });
    }

    // ---- structural guard: merchant audience never exposes PlatformCatalogLink -----------------

    [Fact]
    public void Merchant_Audience_Surface_Never_References_PlatformCatalogLink()
    {
        var forbidden = new[] { typeof(PlatformCatalogLink).Name, "PlatformCatalogLinkReadDto" };
        var contracts = typeof(IPartnerCatalogMerchantAppService).Assembly;

        // Every merchant-audience contract type: the merchant app service + every *Merchant* DTO
        // (browse offering, activation read, service-order merchant view, inputs).
        var merchantSurface = contracts.GetTypes()
            .Where(t => t.IsPublic && (t.FullName?.Contains("Merchant", StringComparison.Ordinal) ?? false))
            .ToList();
        merchantSurface.ShouldNotBeEmpty();

        var visited = new HashSet<Type>();
        foreach (var type in merchantSurface)
        {
            WalkType(type, visited, referenced =>
                forbidden.ShouldNotContain(referenced.Name,
                    $"{type.FullName} reaches {referenced.FullName} — merchant audience must never see platform links"));
        }
    }

    private static void WalkType(Type type, HashSet<Type> visited, Action<Type> onReferenced)
    {
        if (type.IsGenericType)
        {
            foreach (var arg in type.GetGenericArguments())
            {
                WalkType(arg, visited, onReferenced);
            }
        }

        if (!visited.Add(type) || type.Namespace?.StartsWith("Zahy", StringComparison.Ordinal) != true)
        {
            return;
        }

        onReferenced(type);

        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            WalkType(property.PropertyType, visited, onReferenced);
        }

        foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
        {
            WalkType(method.ReturnType, visited, onReferenced);
            foreach (var parameter in method.GetParameters())
            {
                WalkType(parameter.ParameterType, visited, onReferenced);
            }
        }
    }
}
