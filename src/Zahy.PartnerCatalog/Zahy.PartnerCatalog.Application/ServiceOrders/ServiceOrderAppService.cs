using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Zahy.Identity.Partners;
using Zahy.Identity.Permissions;
using Zahy.PartnerCatalog.ServiceOrders;
using Zahy.Settlement;

namespace Zahy.PartnerCatalog;

/// <summary>
/// Gate 2b — service order lifecycle app service. Thin over the ServiceOrder domain: every
/// invariant (state machine, notes, answers, milestones, 7-day auto-accept) lives in the aggregate.
/// Merchant methods are tenant-scoped (IMultiTenant filter), partner methods partner-scoped (query
/// filter) — foreign orders are STRUCTURALLY INVISIBLE (the ratified 2a convention). The merchant
/// price snapshots through the SAME resolution the activation flow uses (activation ResalePrice,
/// defaulting to the offering PartnerCost) — zero new money math. COMPUTE-ONLY: no posting,
/// no invoice, no payment, no escrow, no settlement wiring.
/// </summary>
[Authorize]
public class ServiceOrderAppService : ApplicationService, IServiceOrderAppService
{
    private readonly IRepository<ServiceOrder, Guid> _orders;
    private readonly IRepository<PartnerCatalogItem, Guid> _items;
    private readonly IRepository<PartnerCatalogListing, Guid> _listings;
    private readonly IRepository<MerchantActivation, Guid> _activations;
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentPartner _currentPartner;

    public ServiceOrderAppService(
        IRepository<ServiceOrder, Guid> orders,
        IRepository<PartnerCatalogItem, Guid> items,
        IRepository<PartnerCatalogListing, Guid> listings,
        IRepository<MerchantActivation, Guid> activations,
        ICurrentTenant currentTenant,
        ICurrentPartner currentPartner)
    {
        _orders = orders;
        _items = items;
        _listings = listings;
        _activations = activations;
        _currentTenant = currentTenant;
        _currentPartner = currentPartner;
    }

    // ---- merchant ---------------------------------------------------------------------------------

    [Authorize(ZahyPermissions.Catalog.Read)]
    [UnitOfWork]
    public virtual async Task<MerchantServiceOrderDto> CreateAsync(CreateServiceOrderInput input)
    {
        Check.NotNull(input, nameof(input));
        var tenantId = RequireTenant();

        var item = await _items.GetAsync(input.PartnerCatalogItemId);
        var merchantPrice = await ResolveMerchantPriceAsync(tenantId, item);

        var order = ServiceOrder.Create(
            GuidGenerator.Create(),
            tenantId,
            item,
            merchantPrice,
            input.Milestones
                .OrderBy(m => m.OrderIndex)
                .Select(m => new ServiceOrderMilestoneInput(m.Title, m.Amount))
                .ToList(),
            MerchantActor(),
            Clock.Now.ToUniversalTime());

        await _orders.InsertAsync(order, autoSave: true);
        return ToMerchantDto(order);
    }

    [Authorize(ZahyPermissions.Catalog.Read)]
    [UnitOfWork]
    public virtual async Task<MerchantServiceOrderDto> SubmitRequirementsAsync(SubmitServiceOrderRequirementsInput input)
    {
        Check.NotNull(input, nameof(input));
        var order = await GetOwnOrderAsync(input.OrderId);

        var listing = await FindListingAsync(order.PartnerCatalogItemId);
        order.SubmitRequirements(
            listing?.Requirements ?? (IReadOnlyList<ListingRequirement>)Array.Empty<ListingRequirement>(),
            input.AnswersByRequirementOrderIndex,
            MerchantActor(),
            Clock.Now.ToUniversalTime());

        await _orders.UpdateAsync(order, autoSave: true);
        return ToMerchantDto(order);
    }

    [Authorize(ZahyPermissions.Catalog.Read)]
    [UnitOfWork]
    public virtual async Task<MerchantServiceOrderDto> AcceptDeliveryAsync(ServiceOrderActionInput input)
    {
        var order = await GetOwnOrderAsync(input.OrderId);
        var now = Clock.Now.ToUniversalTime();
        order.MerchantAccept(MerchantActor(), now);
        order.Close(MerchantActor(), now); // immediate in v1 — distinct transition kept for escrow
        await _orders.UpdateAsync(order, autoSave: true);
        return ToMerchantDto(order);
    }

    [Authorize(ZahyPermissions.Catalog.Read)]
    [UnitOfWork]
    public virtual async Task<MerchantServiceOrderDto> RequestRevisionAsync(ServiceOrderActionInput input)
    {
        var order = await GetOwnOrderAsync(input.OrderId);
        order.RequestRevision(MerchantActor(), input.Note ?? string.Empty, Clock.Now.ToUniversalTime());
        await _orders.UpdateAsync(order, autoSave: true);
        return ToMerchantDto(order);
    }

    [Authorize(ZahyPermissions.Catalog.Read)]
    [UnitOfWork]
    public virtual async Task<MerchantServiceOrderDto> CancelAsync(ServiceOrderActionInput input)
    {
        var order = await GetOwnOrderAsync(input.OrderId);
        order.MerchantCancel(MerchantActor(), Clock.Now.ToUniversalTime());
        await _orders.UpdateAsync(order, autoSave: true);
        return ToMerchantDto(order);
    }

    [Authorize(ZahyPermissions.Catalog.Read)]
    public virtual async Task<List<MerchantServiceOrderDto>> GetMyOrdersAsync()
    {
        var tenantId = RequireTenant();
        var queryable = await _orders.GetQueryableAsync();
        return queryable
            .Where(o => o.TenantId == tenantId)
            .OrderByDescending(o => o.CreatedAt)
            .ToList()
            .Select(ToMerchantDto)
            .ToList();
    }

    // ---- partner ----------------------------------------------------------------------------------

    [Authorize(ZahyPermissions.Catalog.Read)]
    [UnitOfWork]
    public virtual async Task<PartnerServiceOrderDto> PartnerAcceptAsync(ServiceOrderActionInput input)
    {
        var order = await GetPartnerOrderAsync(input.OrderId);
        order.PartnerAccept(PartnerActor(), Clock.Now.ToUniversalTime());
        await _orders.UpdateAsync(order, autoSave: true);
        return ToPartnerDto(order);
    }

    [Authorize(ZahyPermissions.Catalog.Read)]
    [UnitOfWork]
    public virtual async Task<PartnerServiceOrderDto> PartnerDeclineAsync(ServiceOrderActionInput input)
    {
        var order = await GetPartnerOrderAsync(input.OrderId);
        order.PartnerDecline(PartnerActor(), input.Note ?? string.Empty, Clock.Now.ToUniversalTime());
        await _orders.UpdateAsync(order, autoSave: true);
        return ToPartnerDto(order);
    }

    [Authorize(ZahyPermissions.Catalog.Read)]
    [UnitOfWork]
    public virtual async Task<PartnerServiceOrderDto> MarkDeliveredAsync(ServiceOrderActionInput input)
    {
        var order = await GetPartnerOrderAsync(input.OrderId);
        order.Deliver(PartnerActor(), Clock.Now.ToUniversalTime());
        await _orders.UpdateAsync(order, autoSave: true);
        return ToPartnerDto(order);
    }

    [Authorize(ZahyPermissions.Catalog.Read)]
    public virtual async Task<List<PartnerServiceOrderDto>> GetIncomingOrdersAsync()
    {
        var partnerId = _currentPartner.Id
            ?? throw new AbpAuthorizationException("Partner context required.");
        var queryable = await _orders.GetQueryableAsync();
        return queryable
            .Where(o => o.PartnerId == partnerId)
            .OrderByDescending(o => o.CreatedAt)
            .ToList()
            .Select(ToPartnerDto)
            .ToList();
    }

    // ---- admin ------------------------------------------------------------------------------------

    /// <summary>v1 auto-accept trigger: a manual/admin sweep (no background-job infra in this gate).
    /// Idempotent — only Delivered-and-due orders transition; re-running sweeps nothing twice.</summary>
    [Authorize(ZahyPermissions.Admin)]
    [UnitOfWork]
    public virtual async Task<int> RunAutoAcceptSweepAsync()
    {
        var now = Clock.Now.ToUniversalTime();
        var queryable = await _orders.GetQueryableAsync();
        var delivered = queryable.Where(o => o.Status == ServiceOrderStatus.Delivered).ToList();

        var accepted = 0;
        foreach (var order in delivered)
        {
            if (order.AutoAcceptIfDue(now))
            {
                await _orders.UpdateAsync(order, autoSave: true);
                accepted++;
            }
        }

        return accepted;
    }

    // ---- helpers ----------------------------------------------------------------------------------

    private Guid RequireTenant() =>
        _currentTenant.Id ?? throw new AbpAuthorizationException("Merchant (tenant) context required.");

    private string MerchantActor() =>
        CurrentUser.Id?.ToString("D") ?? _currentTenant.Id?.ToString("D") ?? "merchant";

    private string PartnerActor() =>
        CurrentUser.Id?.ToString("D") ?? _currentPartner.Id?.ToString("D") ?? "partner";

    private async Task<ServiceOrder> GetOwnOrderAsync(Guid orderId)
    {
        var tenantId = RequireTenant();
        var order = await _orders.GetAsync(orderId);
        if (order.TenantId != tenantId)
        {
            // Belt-and-braces on top of the tenant filter — foreign orders read as absent.
            throw new Volo.Abp.Domain.Entities.EntityNotFoundException(typeof(ServiceOrder), orderId);
        }

        return order;
    }

    private async Task<ServiceOrder> GetPartnerOrderAsync(Guid orderId)
    {
        var partnerId = _currentPartner.Id
            ?? throw new AbpAuthorizationException("Partner context required.");
        var order = await _orders.GetAsync(orderId);
        if (order.PartnerId != partnerId)
        {
            throw new Volo.Abp.Domain.Entities.EntityNotFoundException(typeof(ServiceOrder), orderId);
        }

        return order;
    }

    private async Task<PartnerCatalogListing?> FindListingAsync(Guid itemId)
    {
        var queryable = await _listings.GetQueryableAsync();
        return queryable.FirstOrDefault(x => x.PartnerCatalogItemId == itemId);
    }

    /// <summary>The SAME merchant-price resolution the activation flow uses: the activation's
    /// ResalePrice when one exists for (tenant, offering); otherwise the offering PartnerCost
    /// (ResolveResalePrice's default). Zero new money math.</summary>
    private async Task<Money> ResolveMerchantPriceAsync(Guid tenantId, PartnerCatalogItem item)
    {
        var queryable = await _activations.GetQueryableAsync();
        var activation = queryable.FirstOrDefault(a =>
            a.TenantId == tenantId && a.PartnerCatalogItemId == item.Id);

        return activation?.ResalePrice ?? item.PartnerCost;
    }

    // ---- mapping (fresh literals — structural scoping, the 6b standard) -----------------------------

    private static MerchantServiceOrderDto ToMerchantDto(ServiceOrder order) =>
        new()
        {
            Id = order.Id,
            PartnerCatalogItemId = order.PartnerCatalogItemId,
            OfferingName = order.OfferingNameSnapshot,
            Status = order.Status.ToString(),
            ParticipationMode = order.ParticipationModeSnapshot.ToString(),
            PriceAmount = order.MerchantPriceAmount,          // SELL/fee only — never buy
            Currency = order.Currency,
            RevisionCount = order.RevisionCount,
            CreatedAt = order.CreatedAt,
            DeliveredAt = order.DeliveredAt,
            Answers = MapAnswers(order),
            Milestones = order.Milestones.OrderBy(m => m.OrderIndex).Select(m => new ServiceOrderMilestoneDto
            {
                OrderIndex = m.OrderIndex,
                Title = m.Title,
                Amount = m.Amount
            }).ToList(),
            History = MapHistory(order)
        };

    private static PartnerServiceOrderDto ToPartnerDto(ServiceOrder order) =>
        new()
        {
            Id = order.Id,
            PartnerCatalogItemId = order.PartnerCatalogItemId,
            OfferingName = order.OfferingNameSnapshot,
            Status = order.Status.ToString(),
            ParticipationMode = order.ParticipationModeSnapshot.ToString(),
            BuyAmount = order.BuySnapshotAmount,              // BUY only — never sell/fee
            Currency = order.Currency,
            RevisionCount = order.RevisionCount,
            CreatedAt = order.CreatedAt,
            DeliveredAt = order.DeliveredAt,
            Answers = MapAnswers(order),
            History = MapHistory(order)
        };

    private static List<ServiceOrderAnswerDto> MapAnswers(ServiceOrder order) =>
        order.Answers.OrderBy(a => a.OrderIndex).Select(a => new ServiceOrderAnswerDto
        {
            OrderIndex = a.OrderIndex,
            RequirementTitle = a.RequirementTitleSnapshot,
            RequirementType = a.RequirementType.ToString(),
            AnswerText = a.AnswerText
        }).ToList();

    private static List<ServiceOrderHistoryDto> MapHistory(ServiceOrder order) =>
        order.History.OrderBy(h => h.OrderIndex).Select(h => new ServiceOrderHistoryDto
        {
            OrderIndex = h.OrderIndex,
            Action = h.Action.ToString(),
            FromStatus = h.FromStatus?.ToString(),
            ToStatus = h.ToStatus.ToString(),
            Actor = h.Actor,
            Note = h.Note,
            At = h.At
        }).ToList();
}
