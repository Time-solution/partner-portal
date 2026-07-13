using System;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;

namespace Zahy.PartnerCatalog.Write;

/// <summary>
/// Structured-listing authoring beside the 6a presentation fields. Thin: loads the offering,
/// reuses <see cref="PartnerCatalogWriteAccessGuard.EnsureCanMutateItemAsync"/> (service partners
/// self-author own offerings; delivery/3PL admin-managed; admin any — NO new authoring logic),
/// and delegates every invariant to <see cref="PartnerCatalogListing.ReplaceSections"/>.
/// Presentation-only: no draft gate, no order lifecycle, no money.
/// </summary>
public class PartnerCatalogListingAppService : ApplicationService, IPartnerCatalogListingAppService
{
    private readonly IRepository<PartnerCatalogItem, Guid> _itemRepository;
    private readonly IRepository<PartnerCatalogListing, Guid> _listingRepository;
    private readonly PartnerCatalogWriteAccessGuard _accessGuard;

    public PartnerCatalogListingAppService(
        IRepository<PartnerCatalogItem, Guid> itemRepository,
        IRepository<PartnerCatalogListing, Guid> listingRepository,
        PartnerCatalogWriteAccessGuard accessGuard)
    {
        _itemRepository = itemRepository;
        _listingRepository = listingRepository;
        _accessGuard = accessGuard;
    }

    [UnitOfWork]
    public virtual async Task<PartnerCatalogListingDto> UpdateListingAsync(
        Guid partnerCatalogItemId,
        UpdatePartnerCatalogListingInput input)
    {
        Check.NotNull(input, nameof(input));

        var item = await _itemRepository.GetAsync(partnerCatalogItemId);
        await _accessGuard.EnsureCanMutateItemAsync(item);

        var listing = await FindListingAsync(partnerCatalogItemId);
        var isNew = listing == null;
        listing ??= PartnerCatalogListing.Create(GuidGenerator.Create(), item.Id, item.PartnerId);

        listing.ReplaceSections(
            input.Requirements
                .OrderBy(r => r.OrderIndex)
                .Select(r => new ListingRequirementInput(r.Title, r.Type, r.Choices))
                .ToList(),
            input.Deliverables
                .OrderBy(d => d.OrderIndex)
                .Select(d => new ListingDeliverableInput(d.Title, d.Quantity))
                .ToList(),
            input.ExecutionSteps.OrderBy(s => s.OrderIndex).Select(s => new ListingTextRowInput(s.Text)).ToList(),
            input.Terms.OrderBy(s => s.OrderIndex).Select(s => new ListingTextRowInput(s.Text)).ToList(),
            input.Faqs.OrderBy(f => f.OrderIndex).Select(f => new ListingFaqInput(f.Question, f.Answer)).ToList(),
            Clock.Now.ToUniversalTime());

        if (isNew)
        {
            await _listingRepository.InsertAsync(listing, autoSave: true);
        }
        else
        {
            await _listingRepository.UpdateAsync(listing, autoSave: true);
        }

        return ToDto(listing);
    }

    public virtual async Task<PartnerCatalogListingDto> GetListingAsync(Guid partnerCatalogItemId)
    {
        // Same visibility rule as reading the item itself — loading the item applies the existing
        // partner data filter; a foreign partner simply cannot resolve the item.
        var item = await _itemRepository.GetAsync(partnerCatalogItemId);

        var listing = await FindListingAsync(item.Id);
        return listing == null
            ? new PartnerCatalogListingDto { PartnerCatalogItemId = partnerCatalogItemId }
            : ToDto(listing);
    }

    private async Task<PartnerCatalogListing?> FindListingAsync(Guid itemId)
    {
        var queryable = await _listingRepository.GetQueryableAsync();
        return queryable.FirstOrDefault(x => x.PartnerCatalogItemId == itemId);
    }

    private static PartnerCatalogListingDto ToDto(PartnerCatalogListing listing) =>
        new()
        {
            PartnerCatalogItemId = listing.PartnerCatalogItemId,
            Requirements = listing.Requirements.OrderBy(r => r.OrderIndex).Select(r => new ListingRequirementDto
            {
                OrderIndex = r.OrderIndex,
                Title = r.Title,
                Type = r.Type,
                Choices = r.Choices.ToList()
            }).ToList(),
            Deliverables = listing.Deliverables.OrderBy(d => d.OrderIndex).Select(d => new ListingDeliverableDto
            {
                OrderIndex = d.OrderIndex,
                Title = d.Title,
                Quantity = d.Quantity
            }).ToList(),
            ExecutionSteps = listing.ExecutionSteps.OrderBy(s => s.OrderIndex)
                .Select(s => new ListingTextRowDto { OrderIndex = s.OrderIndex, Text = s.Text }).ToList(),
            Terms = listing.Terms.OrderBy(s => s.OrderIndex)
                .Select(s => new ListingTextRowDto { OrderIndex = s.OrderIndex, Text = s.Text }).ToList(),
            Faqs = listing.Faqs.OrderBy(f => f.OrderIndex).Select(f => new ListingFaqDto
            {
                OrderIndex = f.OrderIndex,
                Question = f.Question,
                Answer = f.Answer
            }).ToList()
        };
}
