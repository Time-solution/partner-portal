using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities;

namespace Zahy.PartnerCatalog;

public class PartnerCatalogItemReflection : Entity<Guid>
{
    public Guid PartnerCatalogItemId { get; private set; }

    /// <summary>Denormalized from the parent item for EF partner query filter (DESIGN Q4).</summary>
    public Guid PartnerId { get; private set; }

    public DateTime VisibleFrom { get; private set; }

    public DateTime? VisibleTo { get; private set; }

    public bool IsPublished { get; private set; }

    public PartnerCatalogReflectionAudience Audience { get; private set; }

    public Guid? PlatformVariantId { get; private set; }

    public int SortOrder { get; private set; }

    protected PartnerCatalogItemReflection()
    {
    }

    public static PartnerCatalogItemReflection Create(
        Guid id,
        Guid partnerCatalogItemId,
        Guid partnerId,
        DateTime visibleFrom,
        DateTime? visibleTo,
        bool isPublished,
        PartnerCatalogReflectionAudience audience = PartnerCatalogReflectionAudience.AllMerchants,
        Guid? platformVariantId = null,
        int sortOrder = 0)
    {
        if (partnerCatalogItemId == Guid.Empty || partnerId == Guid.Empty)
        {
            throw new BusinessException(PartnerCatalogErrorCodes.InvalidReflectionWindow)
                .WithData("Reason", "PartnerAndItemRequired");
        }

        if (visibleTo.HasValue && visibleTo.Value < visibleFrom)
        {
            throw new BusinessException(PartnerCatalogErrorCodes.InvalidReflectionWindow)
                .WithData("VisibleFrom", visibleFrom)
                .WithData("VisibleTo", visibleTo);
        }

        return new PartnerCatalogItemReflection
        {
            Id = id,
            PartnerCatalogItemId = partnerCatalogItemId,
            PartnerId = partnerId,
            VisibleFrom = visibleFrom,
            VisibleTo = visibleTo,
            IsPublished = isPublished,
            Audience = audience,
            PlatformVariantId = platformVariantId,
            SortOrder = sortOrder
        };
    }

    public bool IsVisibleAt(DateTime atUtc) =>
        IsPublished &&
        atUtc >= VisibleFrom &&
        (!VisibleTo.HasValue || atUtc <= VisibleTo.Value);

    public void SetPublished(bool isPublished) => IsPublished = isPublished;

    public void SetVisibilityWindow(DateTime visibleFrom, DateTime? visibleTo)
    {
        if (visibleTo.HasValue && visibleTo.Value < visibleFrom)
        {
            throw new BusinessException(PartnerCatalogErrorCodes.InvalidReflectionWindow);
        }

        VisibleFrom = visibleFrom;
        VisibleTo = visibleTo;
    }
}
