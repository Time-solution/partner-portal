using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;
using Zahy.Identity.Partners;
using Zahy.Identity.Permissions;

namespace Zahy.PartnerCatalog.Read;

public class PartnerCatalogReadAccessGuard : DomainService
{
    private readonly ICurrentPartner _currentPartner;
    private readonly Volo.Abp.Authorization.Permissions.IPermissionChecker _permissionChecker;

    public PartnerCatalogReadAccessGuard(
        ICurrentPartner currentPartner,
        Volo.Abp.Authorization.Permissions.IPermissionChecker permissionChecker)
    {
        _currentPartner = currentPartner;
        _permissionChecker = permissionChecker;
    }

    public async Task<Guid?> ResolvePartnerFilterAsync(Guid? requestedPartnerId)
    {
        if (_currentPartner.Id != null)
        {
            if (requestedPartnerId.HasValue && requestedPartnerId.Value != _currentPartner.Id.Value)
            {
                throw new AbpAuthorizationException("Partner access denied.");
            }

            return _currentPartner.Id.Value;
        }

        if (!await _permissionChecker.IsGrantedAsync(ZahyPermissions.Catalog.Read))
        {
            throw new AbpAuthorizationException("Catalog read permission required.");
        }

        return requestedPartnerId;
    }
}

internal static class PartnerCatalogReadDtoMapper
{
    public static MoneyDto ToMoneyDto(decimal amount, string currency, bool vatInclusive) =>
        new() { Amount = amount, Currency = currency, VatInclusive = vatInclusive };

    public static MoneyDto ToMoneyDto(Settlement.Money money) =>
        ToMoneyDto(money.Amount, money.Currency, money.VatInclusive);

    public static PartnerCatalogItemReadDto ToDto(PartnerCatalogItem item) =>
        new()
        {
            Id = item.Id,
            PartnerId = item.PartnerId,
            Code = item.Code,
            Name = item.Name,
            Description = item.Description,
            MerchantBenefit = item.MerchantBenefit,
            OfferingKind = item.OfferingKind,
            PartnerCost = ToMoneyDto(item.PartnerCost),
            Status = item.Status,
            SettlementBookOverride = item.SettlementBookOverride,
            SettlementTriggerMode = item.SettlementTriggerMode,
            DefaultVatTreatment = item.DefaultVatTreatment,
            ArchivedAt = item.ArchivedAt,
            CarrierServiceCode = item.CarrierServiceCode,
            FulfilmentUnit = item.FulfilmentUnit,
            ExternalMenuItemId = item.ExternalMenuItemId,
            MenuCategoryCode = item.MenuCategoryCode,
            RequiresPlatformCatalogSync = item.RequiresPlatformCatalogSync,
            SettlementParticipationMode = item.SettlementParticipationMode,
            EffectiveSettlementBook = item.EffectiveSettlementBook,
        };

    public static MerchantActivationReadDto ToDto(MerchantActivation activation) =>
        new()
        {
            Id = activation.Id,
            TenantId = activation.TenantId,
            PartnerId = activation.PartnerId,
            PartnerCatalogItemId = activation.PartnerCatalogItemId,
            ResalePrice = ToMoneyDto(activation.ResalePrice),
            Status = activation.Status,
            ActivatedAt = activation.ActivatedAt,
            EndedAt = activation.EndedAt,
            IdempotencyKey = activation.IdempotencyKey,
            ExternalReference = activation.ExternalReference,
        };

    public static SettlementCostMarkupSnapshotReadDto ToDto(SettlementCostMarkupSnapshot snapshot) =>
        new()
        {
            Id = snapshot.Id,
            MerchantActivationId = snapshot.MerchantActivationId,
            PartnerCatalogItemId = snapshot.PartnerCatalogItemId,
            PartnerId = snapshot.PartnerId,
            TenantId = snapshot.TenantId,
            BuyPrice = ToMoneyDto(snapshot.BuyPrice),
            SellPrice = ToMoneyDto(snapshot.SellPrice),
            SellPriceSource = snapshot.SellPriceSource,
            SettlementBook = snapshot.SettlementBook,
            VatTreatment = snapshot.VatTreatment,
            Trigger = snapshot.Trigger,
            ExternalTransactionId = snapshot.ExternalTransactionId,
            OrderLineId = snapshot.OrderLineId,
            SettlementCaseId = snapshot.SettlementCaseId,
            BillingChargeId = snapshot.BillingChargeId,
            CreatedAt = snapshot.CreatedAt,
        };

    public static ReflectedPartnerOrderReadDto ToDto(ReflectedPartnerOrder order) =>
        new()
        {
            Id = order.Id,
            PartnerId = order.PartnerId,
            TenantId = order.TenantId,
            MerchantActivationId = order.MerchantActivationId,
            PartnerCatalogItemId = order.PartnerCatalogItemId,
            SettlementCostMarkupSnapshotId = order.SettlementCostMarkupSnapshotId,
            ExternalTransactionId = order.ExternalTransactionId,
            OrderLineId = order.OrderLineId,
            ReflectedAt = order.ReflectedAt,
        };

    public static PlatformCatalogLinkReadDto ToDto(PlatformCatalogLink link) =>
        new()
        {
            Id = link.Id,
            PartnerCatalogItemId = link.PartnerCatalogItemId,
            TenantId = link.TenantId,
            Status = link.Status,
            PlatformProductId = link.PlatformProductId,
            PlatformVariantId = link.PlatformVariantId,
            LastSyncAttemptAt = link.LastSyncAttemptAt,
            LastSyncError = link.LastSyncError,
            Shape2HandshakeVersion = link.Shape2HandshakeVersion,
        };
}

[Authorize(ZahyPermissions.Catalog.Read)]
public class PartnerCatalogReadAppService : ApplicationService, IPartnerCatalogReadAppService
{
    private readonly IRepository<PartnerCatalogItem, Guid> _itemRepository;
    private readonly IRepository<MerchantActivation, Guid> _activationRepository;
    private readonly IRepository<SettlementCostMarkupSnapshot, Guid> _snapshotRepository;
    private readonly IRepository<ReflectedPartnerOrder, Guid> _reflectedOrderRepository;
    private readonly IRepository<PlatformCatalogLink, Guid> _platformLinkRepository;
    private readonly PartnerCatalogReadAccessGuard _accessGuard;

    public PartnerCatalogReadAppService(
        IRepository<PartnerCatalogItem, Guid> itemRepository,
        IRepository<MerchantActivation, Guid> activationRepository,
        IRepository<SettlementCostMarkupSnapshot, Guid> snapshotRepository,
        IRepository<ReflectedPartnerOrder, Guid> reflectedOrderRepository,
        IRepository<PlatformCatalogLink, Guid> platformLinkRepository,
        PartnerCatalogReadAccessGuard accessGuard)
    {
        _itemRepository = itemRepository;
        _activationRepository = activationRepository;
        _snapshotRepository = snapshotRepository;
        _reflectedOrderRepository = reflectedOrderRepository;
        _platformLinkRepository = platformLinkRepository;
        _accessGuard = accessGuard;
    }

    public async Task<List<PartnerCatalogItemReadDto>> GetCatalogItemsAsync(PartnerCatalogItemsQuery query)
    {
        var partnerId = await _accessGuard.ResolvePartnerFilterAsync(query.PartnerId);
        var items = await _itemRepository.GetListAsync(x =>
            !partnerId.HasValue || x.PartnerId == partnerId.Value);

        return items
            .OrderBy(x => x.Code)
            .Select(PartnerCatalogReadDtoMapper.ToDto)
            .ToList();
    }

    public async Task<List<MerchantActivationReadDto>> GetActivationsAsync(MerchantActivationsQuery query)
    {
        var partnerId = await _accessGuard.ResolvePartnerFilterAsync(query.PartnerId);
        var activations = await _activationRepository.GetListAsync(x =>
            (!partnerId.HasValue || x.PartnerId == partnerId.Value) &&
            (!query.TenantId.HasValue || x.TenantId == query.TenantId.Value));

        return activations
            .OrderByDescending(x => x.CreationTime)
            .Select(PartnerCatalogReadDtoMapper.ToDto)
            .ToList();
    }

    public async Task<List<SettlementCostMarkupSnapshotReadDto>> GetSnapshotsAsync(PartnerCatalogPartnerQuery query)
    {
        var partnerId = await _accessGuard.ResolvePartnerFilterAsync(query.PartnerId);
        var snapshots = await _snapshotRepository.GetListAsync(x =>
            !partnerId.HasValue || x.PartnerId == partnerId.Value);

        return snapshots
            .OrderByDescending(x => x.CreatedAt)
            .Select(PartnerCatalogReadDtoMapper.ToDto)
            .ToList();
    }

    public async Task<List<ReflectedPartnerOrderReadDto>> GetReflectedOrdersAsync(PartnerCatalogPartnerQuery query)
    {
        var partnerId = await _accessGuard.ResolvePartnerFilterAsync(query.PartnerId);
        var orders = await _reflectedOrderRepository.GetListAsync(x =>
            !partnerId.HasValue || x.PartnerId == partnerId.Value);

        return orders
            .OrderByDescending(x => x.ReflectedAt)
            .Select(PartnerCatalogReadDtoMapper.ToDto)
            .ToList();
    }

    public async Task<List<PlatformCatalogLinkReadDto>> GetPlatformCatalogLinksAsync(PlatformCatalogLinksQuery query)
    {
        var partnerId = await _accessGuard.ResolvePartnerFilterAsync(query.PartnerId);
        var itemQueryable = await _itemRepository.GetQueryableAsync();
        var allowedItemIds = partnerId.HasValue
            ? itemQueryable.Where(x => x.PartnerId == partnerId.Value).Select(x => x.Id).ToHashSet()
            : null;

        var links = await _platformLinkRepository.GetListAsync(x =>
            (!query.PartnerCatalogItemId.HasValue || x.PartnerCatalogItemId == query.PartnerCatalogItemId.Value) &&
            (allowedItemIds == null || allowedItemIds.Contains(x.PartnerCatalogItemId)));

        return links
            .OrderBy(x => x.PartnerCatalogItemId)
            .Select(PartnerCatalogReadDtoMapper.ToDto)
            .ToList();
    }
}
