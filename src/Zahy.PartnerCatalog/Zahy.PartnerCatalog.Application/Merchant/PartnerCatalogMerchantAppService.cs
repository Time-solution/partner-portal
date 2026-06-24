using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Zahy.Identity.Permissions;
using Zahy.PartnerCatalog.Read;
using Zahy.Settlement;

namespace Zahy.PartnerCatalog.Merchant;

[Authorize]
public class PartnerCatalogMerchantAppService : ApplicationService, IPartnerCatalogMerchantAppService
{
    private readonly IRepository<PartnerCatalogItem, Guid> _itemRepository;
    private readonly IRepository<MerchantActivation, Guid> _activationRepository;
    private readonly IRepository<PartnerCatalogProfile, Guid> _profileRepository;
    private readonly MerchantCatalogAccessGuard _accessGuard;
    private readonly PartnerCatalogActivationSnapshotOrchestrator _activationSnapshotOrchestrator;
    private readonly IOptionsMonitor<PartnerCatalogMerchantOptions> _merchantOptions;

    public PartnerCatalogMerchantAppService(
        IRepository<PartnerCatalogItem, Guid> itemRepository,
        IRepository<MerchantActivation, Guid> activationRepository,
        IRepository<PartnerCatalogProfile, Guid> profileRepository,
        MerchantCatalogAccessGuard accessGuard,
        PartnerCatalogActivationSnapshotOrchestrator activationSnapshotOrchestrator,
        IOptionsMonitor<PartnerCatalogMerchantOptions> merchantOptions)
    {
        _itemRepository = itemRepository;
        _activationRepository = activationRepository;
        _profileRepository = profileRepository;
        _accessGuard = accessGuard;
        _activationSnapshotOrchestrator = activationSnapshotOrchestrator;
        _merchantOptions = merchantOptions;
    }

    [Authorize(ZahyPermissions.Catalog.Read)]
    public async Task<List<MerchantPartnerOfferingReadDto>> GetAvailableOfferingsAsync()
    {
        _ = await _accessGuard.GetRequiredMerchantTenantIdAsync();

        var items = await _itemRepository.GetListAsync(x => x.Status == PartnerCatalogItemStatus.Active);

        // Phase 6a — partner-level brief is one row per partner; resolve once and attach read-only.
        var profiles = await _profileRepository.GetListAsync();
        var briefByPartner = profiles
            .Where(p => !string.IsNullOrWhiteSpace(p.PartnerBrief))
            .ToDictionary(p => p.PartnerId, p => p.PartnerBrief);

        return items
            .OrderBy(x => x.PartnerId)
            .ThenBy(x => x.Code)
            .Select(item => ToOfferingDto(item, briefByPartner))
            .ToList();
    }

    [Authorize(ZahyPermissions.Catalog.Read)]
    public async Task<List<MerchantActivationReadDto>> GetMyActivationsAsync(
        MerchantActivationsForTenantQuery query)
    {
        var tenantId = query.TenantId ?? await _accessGuard.GetRequiredMerchantTenantIdAsync();
        await _accessGuard.EnsureCanAccessTenantAsync(tenantId);

        var activations = await _activationRepository.GetListAsync(x => x.TenantId == tenantId);

        return activations
            .OrderByDescending(x => x.CreationTime)
            .Select(PartnerCatalogReadDtoMapper.ToDto)
            .ToList();
    }

    [Authorize(ZahyPermissions.Catalog.Write)]
    public async Task<MerchantActivationReadDto> ActivateAsync(ActivateMerchantOfferingInput input)
    {
        Check.NotNull(input, nameof(input));
        var tenantId = await _accessGuard.GetRequiredMerchantTenantIdAsync();

        var item = await _itemRepository.FindAsync(input.PartnerCatalogItemId);
        if (item == null || item.Status != PartnerCatalogItemStatus.Active)
        {
            throw new BusinessException(PartnerCatalogErrorCodes.ItemNotActive)
                .WithData("PartnerCatalogItemId", input.PartnerCatalogItemId);
        }

        var existing = await FindOpenActivationAsync(tenantId, item.Id);
        if (existing != null)
        {
            return PartnerCatalogReadDtoMapper.ToDto(existing);
        }

        var ended = await _activationRepository.FirstOrDefaultAsync(x =>
            x.TenantId == tenantId &&
            x.PartnerCatalogItemId == item.Id &&
            x.Status == MerchantActivationStatus.Ended);

        if (ended != null)
        {
            throw new BusinessException(PartnerCatalogErrorCodes.ActivationAlreadyEnded)
                .WithData("ActivationId", ended.Id);
        }

        var resalePrice = ResolveResalePrice(input.ResalePrice, item);
        var activation = MerchantActivation.Create(
            GuidGenerator.Create(),
            tenantId,
            item,
            resalePrice,
            input.ExternalReference);

        await _activationRepository.InsertAsync(activation, autoSave: true);

        var atUtc = Clock.Now.ToUniversalTime();
        activation.Activate(atUtc);
        await _activationRepository.UpdateAsync(activation, autoSave: true);

        // Participation / subscription bridges stay OFF by default (PartnerCatalogMerchantOptions).
        // When ParticipationBridgeEnabled is ON, snapshot-on-activate routes through the existing bridge.
        await _activationSnapshotOrchestrator.TryDispatchOnActivateAsync(
            activation,
            item,
            _merchantOptions.CurrentValue);

        return PartnerCatalogReadDtoMapper.ToDto(activation);
    }

    [Authorize(ZahyPermissions.Catalog.Write)]
    public async Task<MerchantActivationReadDto> DeactivateAsync(Guid activationId)
    {
        var activation = await _activationRepository.FindAsync(activationId);
        if (activation == null)
        {
            throw new BusinessException(PartnerCatalogErrorCodes.ActivationNotFound)
                .WithData("ActivationId", activationId);
        }

        await _accessGuard.EnsureCanMutateActivationAsync(activation);

        var atUtc = Clock.Now.ToUniversalTime();
        if (activation.Status == MerchantActivationStatus.Pending)
        {
            activation.Cancel(atUtc);
        }
        else
        {
            activation.End(atUtc);
        }

        await _activationRepository.UpdateAsync(activation, autoSave: true);

        return PartnerCatalogReadDtoMapper.ToDto(activation);
    }

    private async Task<MerchantActivation?> FindOpenActivationAsync(Guid tenantId, Guid catalogItemId)
    {
        return await _activationRepository.FirstOrDefaultAsync(x =>
            x.TenantId == tenantId &&
            x.PartnerCatalogItemId == catalogItemId &&
            x.Status != MerchantActivationStatus.Ended);
    }

    private static Money ResolveResalePrice(MoneyDto? input, PartnerCatalogItem item)
    {
        if (input == null)
        {
            return item.PartnerCost;
        }

        return Money.Of(input.Amount, input.Currency, input.VatInclusive);
    }

    private static MerchantPartnerOfferingReadDto ToOfferingDto(
        PartnerCatalogItem item,
        IReadOnlyDictionary<Guid, string?> briefByPartner) =>
        new()
        {
            Id = item.Id,
            PartnerId = item.PartnerId,
            Code = item.Code,
            Name = item.Name,
            // Description doubles as the merchant-facing OfferingSummary (reused, not duplicated).
            Description = item.Description,
            PartnerBrief = briefByPartner.TryGetValue(item.PartnerId, out var brief) ? brief : null,
            MerchantBenefit = item.MerchantBenefit,
            OfferingKind = item.OfferingKind,
            PartnerCost = PartnerCatalogReadDtoMapper.ToMoneyDto(item.PartnerCost),
            SettlementParticipationMode = item.SettlementParticipationMode,
            SettlementTriggerMode = item.SettlementTriggerMode,
        };
}
