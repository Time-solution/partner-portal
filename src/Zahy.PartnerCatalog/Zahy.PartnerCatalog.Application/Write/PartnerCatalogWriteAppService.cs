using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Zahy.Identity.Permissions;
using Zahy.PartnerCatalog.Read;
using Zahy.Settlement;

namespace Zahy.PartnerCatalog.Write;

[Authorize]
public class PartnerCatalogWriteAppService : ApplicationService, IPartnerCatalogWriteAppService
{
    private readonly IRepository<PartnerCatalogItem, Guid> _itemRepository;
    private readonly PartnerCatalogWriteAccessGuard _accessGuard;

    public PartnerCatalogWriteAppService(
        IRepository<PartnerCatalogItem, Guid> itemRepository,
        PartnerCatalogWriteAccessGuard accessGuard)
    {
        _itemRepository = itemRepository;
        _accessGuard = accessGuard;
    }

    public async Task<PartnerCatalogItemReadDto> CreateAsync(CreatePartnerCatalogItemInput input)
    {
        Check.NotNull(input, nameof(input));
        await _accessGuard.EnsureCanAuthorAsync(input.PartnerId, input.OfferingKind);
        await EnsureUniqueCodeAsync(input.PartnerId, input.Code);

        var item = PartnerCatalogItem.Create(
            GuidGenerator.Create(),
            input.PartnerId,
            input.Code,
            input.Name,
            input.Description,
            input.OfferingKind,
            ToMoney(input.PartnerCost),
            input.SettlementBookOverride,
            input.SettlementTriggerMode,
            input.CarrierServiceCode,
            input.FulfilmentUnit,
            input.ExternalMenuItemId,
            input.MenuCategoryCode,
            input.SettlementParticipationMode,
            input.ConsignmentOwnershipMode,
            input.MerchantBenefit);

        await _itemRepository.InsertAsync(item, autoSave: true);

        // Snapshot/settlement bridges stay OFF — no side effects on save.

        return PartnerCatalogReadDtoMapper.ToDto(item);
    }

    public async Task<PartnerCatalogItemReadDto> UpdateAsync(Guid id, UpdatePartnerCatalogItemInput input)
    {
        Check.NotNull(input, nameof(input));
        var item = await RequireItemAsync(id);
        await _accessGuard.EnsureCanMutateItemAsync(item);

        item.UpdateDraft(
            input.Name,
            input.Description,
            ToMoney(input.PartnerCost),
            input.SettlementBookOverride,
            input.SettlementTriggerMode,
            input.CarrierServiceCode,
            input.FulfilmentUnit,
            input.ExternalMenuItemId,
            input.MenuCategoryCode,
            input.SettlementParticipationMode,
            input.ConsignmentOwnershipMode,
            input.MerchantBenefit);

        await _itemRepository.UpdateAsync(item, autoSave: true);

        return PartnerCatalogReadDtoMapper.ToDto(item);
    }

    public async Task<PartnerCatalogItemReadDto> PublishAsync(Guid id)
    {
        var item = await RequireItemAsync(id);
        await _accessGuard.EnsureCanMutateItemAsync(item);

        item.Publish(Clock.Now.ToUniversalTime());
        await _itemRepository.UpdateAsync(item, autoSave: true);

        return PartnerCatalogReadDtoMapper.ToDto(item);
    }

    public async Task<PartnerCatalogItemReadDto> ArchiveAsync(Guid id)
    {
        var item = await RequireItemAsync(id);
        await _accessGuard.EnsureCanMutateItemAsync(item);

        item.Archive(Clock.Now.ToUniversalTime());
        await _itemRepository.UpdateAsync(item, autoSave: true);

        return PartnerCatalogReadDtoMapper.ToDto(item);
    }

    private async Task<PartnerCatalogItem> RequireItemAsync(Guid id)
    {
        var item = await _itemRepository.FindAsync(id);
        if (item == null)
        {
            throw new BusinessException(PartnerCatalogErrorCodes.CatalogItemNotFound)
                .WithData("PartnerCatalogItemId", id);
        }

        return item;
    }

    private async Task EnsureUniqueCodeAsync(Guid partnerId, string code)
    {
        var normalized = code.Trim();
        var existing = await _itemRepository.FirstOrDefaultAsync(x =>
            x.PartnerId == partnerId && x.Code == normalized);

        if (existing != null)
        {
            throw new BusinessException(PartnerCatalogErrorCodes.DuplicateCatalogCode)
                .WithData("PartnerId", partnerId)
                .WithData("Code", normalized);
        }
    }

    private static Money ToMoney(MoneyDto dto) =>
        Money.Of(dto.Amount, dto.Currency, dto.VatInclusive);
}
