using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Shouldly;
using Volo.Abp;
using Xunit;
using Zahy.Identity.Permissions;
using Zahy.PartnerCatalog.Write;
using Zahy.PartnerPlatform.Partners;

namespace Zahy.PartnerCatalog;

public class PartnerCatalogWriteAppServiceTests : ZahyPartnerCatalogTestBase
{
    private static readonly Guid ServicePartnerId = Guid.Parse("22222222-2222-2222-2222-222222222004");
    private static readonly Guid DeliveryPartnerId = Guid.Parse("22222222-2222-2222-2222-222222222001");

    [Fact]
    public async Task Service_Partner_Can_Create_Edit_Publish_Own_Items()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            ConfigureSelfServiceAuthor(ServicePartnerId, PartnerType.Service);

            var write = GetRequiredService<IPartnerCatalogWriteAppService>();
            var created = await write.CreateAsync(new CreatePartnerCatalogItemInput
            {
                PartnerId = ServicePartnerId,
                Code = "SVC-BASIC",
                Name = "Basic tier",
                OfferingKind = PartnerCatalogOfferingKind.ServiceOneOff,
                PartnerCost = new Read.MoneyDto { Amount = 49m, Currency = "SAR", VatInclusive = true },
            });

            created.Status.ShouldBe(PartnerCatalogItemStatus.Draft);
            created.PartnerCost.Amount.ShouldBe(49m);

            var updated = await write.UpdateAsync(created.Id, new UpdatePartnerCatalogItemInput
            {
                Name = "Basic tier (updated)",
                PartnerCost = new Read.MoneyDto { Amount = 59m, Currency = "SAR", VatInclusive = true },
            });
            updated.Name.ShouldBe("Basic tier (updated)");
            updated.PartnerCost.Amount.ShouldBe(59m);

            var published = await write.PublishAsync(created.Id);
            published.Status.ShouldBe(PartnerCatalogItemStatus.Active);
        });
    }

    [Fact]
    public async Task Service_Partner_Cannot_Author_Delivery_Type_Catalog()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            ConfigureSelfServiceAuthor(ServicePartnerId, PartnerType.Service);

            var write = GetRequiredService<IPartnerCatalogWriteAppService>();
            var ex = await Should.ThrowAsync<BusinessException>(() =>
                write.CreateAsync(new CreatePartnerCatalogItemInput
                {
                    PartnerId = ServicePartnerId,
                    Code = "DEL-BAD",
                    Name = "Illegal delivery item",
                    OfferingKind = PartnerCatalogOfferingKind.DeliveryFulfilmentPerOrder,
                    PartnerCost = new Read.MoneyDto { Amount = 10m, Currency = "SAR", VatInclusive = true },
                    FulfilmentUnit = PartnerCatalogFulfilmentUnit.PerShipment,
                }));

            ex.Code.ShouldBe(PartnerCatalogErrorCodes.AuthoringNotPermitted);
        });
    }

    [Fact]
    public async Task Delivery_Partner_Cannot_Self_Author()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            ConfigureSelfServiceAuthor(DeliveryPartnerId, PartnerType.Carrier);

            var write = GetRequiredService<IPartnerCatalogWriteAppService>();
            var ex = await Should.ThrowAsync<BusinessException>(() =>
                write.CreateAsync(new CreatePartnerCatalogItemInput
                {
                    PartnerId = DeliveryPartnerId,
                    Code = "DEL-SELF",
                    Name = "Self-authored delivery",
                    OfferingKind = PartnerCatalogOfferingKind.DeliveryFulfilmentPerOrder,
                    PartnerCost = new Read.MoneyDto { Amount = 10m, Currency = "SAR", VatInclusive = true },
                    FulfilmentUnit = PartnerCatalogFulfilmentUnit.PerShipment,
                }));

            ex.Code.ShouldBe(PartnerCatalogErrorCodes.AuthoringNotPermitted);
        });
    }

    [Fact]
    public async Task Admin_Can_Author_Delivery_Partner_Purchase_Agreement()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            ConfigureManagedAuthor(DeliveryPartnerId, PartnerType.Carrier);

            var write = GetRequiredService<IPartnerCatalogWriteAppService>();
            var created = await write.CreateAsync(new CreatePartnerCatalogItemInput
            {
                PartnerId = DeliveryPartnerId,
                Code = "DEL-ADMIN",
                Name = "Admin purchase agreement",
                OfferingKind = PartnerCatalogOfferingKind.DeliveryFulfilmentPerOrder,
                PartnerCost = new Read.MoneyDto { Amount = 12m, Currency = "SAR", VatInclusive = true },
                FulfilmentUnit = PartnerCatalogFulfilmentUnit.PerShipment,
            });

            created.PartnerId.ShouldBe(DeliveryPartnerId);
            created.PartnerCost.Amount.ShouldBe(12m);
            created.OfferingKind.ShouldBe(PartnerCatalogOfferingKind.DeliveryFulfilmentPerOrder);
        });
    }

    [Fact]
    public async Task Save_Does_Not_Create_Snapshot()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            ConfigureSelfServiceAuthor(ServicePartnerId, PartnerType.Service);

            var snapshotRepo = GetRequiredService<Volo.Abp.Domain.Repositories.IRepository<SettlementCostMarkupSnapshot, Guid>>();
            var before = await snapshotRepo.GetCountAsync();

            var write = GetRequiredService<IPartnerCatalogWriteAppService>();
            var created = await write.CreateAsync(new CreatePartnerCatalogItemInput
            {
                PartnerId = ServicePartnerId,
                Code = "SVC-NOSNAP",
                Name = "No snapshot",
                OfferingKind = PartnerCatalogOfferingKind.ServiceSubscription,
                PartnerCost = new Read.MoneyDto { Amount = 70m, Currency = "SAR", VatInclusive = true },
            });
            await write.PublishAsync(created.Id);

            var after = await snapshotRepo.GetCountAsync();
            after.ShouldBe(before);
        });
    }

    [Fact]
    public async Task Cross_Partner_Self_Author_Is_Blocked()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            ConfigureSelfServiceAuthor(ServicePartnerId, PartnerType.Service);
            GetRequiredService<PartnerCatalogTestPartnerTypeLookup>().Set(DeliveryPartnerId, PartnerType.Carrier);

            var write = GetRequiredService<IPartnerCatalogWriteAppService>();
            await Should.ThrowAsync<Volo.Abp.Authorization.AbpAuthorizationException>(() =>
                write.CreateAsync(new CreatePartnerCatalogItemInput
                {
                    PartnerId = DeliveryPartnerId,
                    Code = "X-PARTNER",
                    Name = "Cross partner",
                    OfferingKind = PartnerCatalogOfferingKind.ServiceOneOff,
                    PartnerCost = new Read.MoneyDto { Amount = 1m, Currency = "SAR", VatInclusive = true },
                }));
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

public sealed class PartnerCatalogTestPartnerTypeLookup : IPartnerCatalogPartnerTypeLookup
{
    private readonly Dictionary<Guid, PartnerType> _types = new();

    public void Set(Guid partnerId, PartnerType partnerType) => _types[partnerId] = partnerType;

    public Task<PartnerType?> GetPartnerTypeAsync(Guid partnerId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_types.TryGetValue(partnerId, out var type) ? type : (PartnerType?)null);
}

public sealed class PartnerCatalogTestPermissionChecker : Volo.Abp.Authorization.Permissions.IPermissionChecker
{
    private HashSet<string> _granted = new(StringComparer.Ordinal);

    public void GrantOnly(params string[] permissions)
    {
        _granted = new HashSet<string>(permissions, StringComparer.Ordinal);
    }

    public Task<bool> IsGrantedAsync(string name) =>
        Task.FromResult(_granted.Count == 0 || _granted.Contains(name));

    public Task<bool> IsGrantedAsync(System.Security.Claims.ClaimsPrincipal? claimsPrincipal, string name) =>
        IsGrantedAsync(name);

    public Task<Volo.Abp.Authorization.Permissions.MultiplePermissionGrantResult> IsGrantedAsync(string[] names)
    {
        var result = new Volo.Abp.Authorization.Permissions.MultiplePermissionGrantResult();
        foreach (var name in names)
        {
            result.Result[name] = _granted.Count == 0 || _granted.Contains(name)
                ? Volo.Abp.Authorization.Permissions.PermissionGrantResult.Granted
                : Volo.Abp.Authorization.Permissions.PermissionGrantResult.Prohibited;
        }

        return Task.FromResult(result);
    }

    public Task<Volo.Abp.Authorization.Permissions.MultiplePermissionGrantResult> IsGrantedAsync(
        System.Security.Claims.ClaimsPrincipal? claimsPrincipal,
        string[] names) => IsGrantedAsync(names);
}
