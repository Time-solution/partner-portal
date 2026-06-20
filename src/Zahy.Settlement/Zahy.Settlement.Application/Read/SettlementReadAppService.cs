using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;
using Zahy.Commission;
using Zahy.Identity.Partners;
using Zahy.Identity.Permissions;
using Zahy.PartnerCatalog;

namespace Zahy.Settlement.Read;

public class SettlementReadAccessGuard : DomainService
{
    private readonly ICurrentPartner _currentPartner;
    private readonly Volo.Abp.Authorization.Permissions.IPermissionChecker _permissionChecker;

    public SettlementReadAccessGuard(
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

        if (!await _permissionChecker.IsGrantedAsync(ZahyPermissions.Settlement.Read))
        {
            throw new AbpAuthorizationException("Settlement read permission required.");
        }

        return requestedPartnerId;
    }
}

internal static class SettlementReadDtoMapper
{
    public static MoneyDto ToMoneyDto(decimal amount, string currency, bool vatInclusive) =>
        new() { Amount = amount, Currency = currency, VatInclusive = vatInclusive };

    public static MoneyDto ToMoneyDto(Money money) =>
        ToMoneyDto(money.Amount, money.Currency, money.VatInclusive);

    public static SettlementJournalReadDto? ToJournalDto(
        SettlementAllocationSnapshot? snapshot,
        DateTime? postedAt)
    {
        if (snapshot == null)
        {
            return null;
        }

        return new SettlementJournalReadDto
        {
            Currency = snapshot.Currency,
            PostedAt = postedAt,
            TotalDebits = ToMoneyDto(snapshot.TotalDebits, snapshot.Currency, vatInclusive: false),
            TotalCredits = ToMoneyDto(snapshot.TotalCredits, snapshot.Currency, vatInclusive: false),
            Lines = snapshot.Legs.Select(leg => new SettlementJournalLineReadDto
            {
                Account = Enum.Parse<SettlementAccountType>(leg.Account, ignoreCase: true),
                Direction = Enum.Parse<EntryDirection>(leg.Direction, ignoreCase: true),
                Amount = ToMoneyDto(leg.Amount, snapshot.Currency, vatInclusive: false),
            }).ToList(),
        };
    }

    public static SettlementCaseReadDto ToDto(SettlementCase settlementCase, SettlementJournalReadDto? journal) =>
        new()
        {
            Id = settlementCase.Id,
            Book = settlementCase.Book,
            PartnerId = settlementCase.PartnerId,
            ExternalTransactionId = settlementCase.ExternalTransactionId,
            State = settlementCase.State,
            CreatedAt = settlementCase.CreatedAt,
            UpdatedAt = settlementCase.UpdatedAt,
            ReversesSettlementCaseId = settlementCase.ReversesSettlementCaseId,
            Journal = journal,
        };

    public static SettlementBillingChargeReadDto ToDto(
        BillingCharge charge,
        IReadOnlyList<Guid> linkedSnapshotIds) =>
        new()
        {
            Id = charge.Id,
            PartnerId = charge.PartnerId,
            TenantId = charge.TenantId,
            ChargeTarget = charge.ChargeTarget,
            Kind = charge.Kind,
            Amount = ToMoneyDto(charge.Amount, charge.Currency, vatInclusive: true),
            IdempotencyKey = charge.IdempotencyKey,
            PeriodKey = charge.PeriodKey,
            Description = charge.Description,
            ChargedAt = charge.ChargedAt,
            LinkedSnapshotIds = linkedSnapshotIds.ToList(),
        };
}

[Authorize(ZahyPermissions.Settlement.Read)]
public class SettlementReadAppService : ApplicationService, ISettlementReadAppService
{
    private readonly IRepository<SettlementCase, Guid> _caseRepository;
    private readonly ISettlementEventStore _eventStore;
    private readonly IRepository<BillingCharge, Guid> _billingChargeRepository;
    private readonly IRepository<SettlementCostMarkupSnapshot, Guid> _snapshotRepository;
    private readonly SettlementReadAccessGuard _accessGuard;

    public SettlementReadAppService(
        IRepository<SettlementCase, Guid> caseRepository,
        ISettlementEventStore eventStore,
        IRepository<BillingCharge, Guid> billingChargeRepository,
        IRepository<SettlementCostMarkupSnapshot, Guid> snapshotRepository,
        SettlementReadAccessGuard accessGuard)
    {
        _caseRepository = caseRepository;
        _eventStore = eventStore;
        _billingChargeRepository = billingChargeRepository;
        _snapshotRepository = snapshotRepository;
        _accessGuard = accessGuard;
    }

    public async Task<List<SettlementCaseReadDto>> GetCasesAsync(SettlementPartnerQuery query)
    {
        var partnerId = await _accessGuard.ResolvePartnerFilterAsync(query.PartnerId);
        var cases = await _caseRepository.GetListAsync(x =>
            x.ReversesSettlementCaseId == null &&
            (!partnerId.HasValue || x.PartnerId == partnerId.Value));

        var results = new List<SettlementCaseReadDto>();
        foreach (var settlementCase in cases.OrderByDescending(x => x.CreatedAt))
        {
            results.Add(await MapCaseAsync(settlementCase));
        }

        return results;
    }

    public async Task<List<SettlementCaseReadDto>> GetReversalsAsync(SettlementPartnerQuery query)
    {
        var partnerId = await _accessGuard.ResolvePartnerFilterAsync(query.PartnerId);
        var cases = await _caseRepository.GetListAsync(x =>
            x.ReversesSettlementCaseId != null &&
            (!partnerId.HasValue || x.PartnerId == partnerId.Value));

        var results = new List<SettlementCaseReadDto>();
        foreach (var settlementCase in cases.OrderByDescending(x => x.CreatedAt))
        {
            results.Add(await MapCaseAsync(settlementCase));
        }

        return results;
    }

    public async Task<List<SettlementBillingChargeReadDto>> GetBillingChargesAsync(SettlementPartnerQuery query)
    {
        var partnerId = await _accessGuard.ResolvePartnerFilterAsync(query.PartnerId);
        var charges = await _billingChargeRepository.GetListAsync(x =>
            x.Kind == BillingChargeKind.Subscription &&
            (!partnerId.HasValue || x.PartnerId == partnerId.Value));

        var snapshotQueryable = await _snapshotRepository.GetQueryableAsync();
        var snapshotLinks = snapshotQueryable
            .Where(x => x.BillingChargeId != null)
            .GroupBy(x => x.BillingChargeId!.Value)
            .ToDictionary(g => g.Key, g => g.Select(s => s.Id).ToList());

        return charges
            .OrderByDescending(x => x.ChargedAt)
            .Select(charge => SettlementReadDtoMapper.ToDto(
                charge,
                snapshotLinks.TryGetValue(charge.Id, out var ids) ? ids : Array.Empty<Guid>()))
            .ToList();
    }

    private async Task<SettlementCaseReadDto> MapCaseAsync(SettlementCase settlementCase)
    {
        var events = await _eventStore.GetByCaseAsync(settlementCase.Id);
        var allocationJson = events
            .Where(e => e.Outcome == SettlementEventOutcome.Allocated && e.AllocationSnapshotJson != null)
            .Select(e => e.AllocationSnapshotJson)
            .FirstOrDefault();
        var postedAt = events
            .Where(e => e.Outcome == SettlementEventOutcome.Allocated)
            .Select(e => (DateTime?)e.ReceivedAt)
            .FirstOrDefault();

        var journal = SettlementReadDtoMapper.ToJournalDto(
            SettlementAllocationSnapshot.FromJson(allocationJson),
            postedAt);

        return SettlementReadDtoMapper.ToDto(settlementCase, journal);
    }
}
