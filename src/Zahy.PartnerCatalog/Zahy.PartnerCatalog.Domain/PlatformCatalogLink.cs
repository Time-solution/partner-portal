using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities;

namespace Zahy.PartnerCatalog;

/// <summary>
/// Described boundary to main-platform catalog (Pattern B / FnB only). Shape 1: DeferredShape2, no sync.
/// </summary>
public class PlatformCatalogLink : Entity<Guid>
{
    public Guid PartnerCatalogItemId { get; private set; }

    public Guid? TenantId { get; private set; }

    public PlatformCatalogLinkStatus Status { get; private set; }

    public Guid? PlatformProductId { get; private set; }

    /// <summary>Never populated in Step 2a — Shape 2 sync only.</summary>
    public Guid? PlatformVariantId { get; private set; }

    public DateTime? LastSyncAttemptAt { get; private set; }

    public string? LastSyncError { get; private set; }

    public string? Shape2HandshakeVersion { get; private set; }

    protected PlatformCatalogLink()
    {
    }

    /// <summary>Shape 1 factory — always <see cref="PlatformCatalogLinkStatus.DeferredShape2"/>; no sync logic.</summary>
    public static PlatformCatalogLink CreateDeferredShape2(
        Guid id,
        PartnerCatalogItem catalogItem,
        Guid? tenantId = null,
        string? shape2HandshakeVersion = "catalog-sync-v0-described-only")
    {
        Check.NotNull(catalogItem, nameof(catalogItem));

        if (catalogItem.OfferingKind != PartnerCatalogOfferingKind.FnBItemsPerSale)
        {
            throw new BusinessException(PartnerCatalogErrorCodes.InvalidOfferingKind)
                .WithData("Reason", "PlatformCatalogLinkRequiresFnBItem");
        }

        if (!catalogItem.RequiresPlatformCatalogSync)
        {
            throw new BusinessException(PartnerCatalogErrorCodes.InvalidOfferingKind)
                .WithData("Reason", "PlatformCatalogLinkRequiresSyncFlag");
        }

        return new PlatformCatalogLink
        {
            Id = id,
            PartnerCatalogItemId = catalogItem.Id,
            TenantId = tenantId,
            Status = PlatformCatalogLinkStatus.DeferredShape2,
            PlatformProductId = null,
            PlatformVariantId = null,
            LastSyncAttemptAt = null,
            LastSyncError = null,
            Shape2HandshakeVersion = shape2HandshakeVersion?.Trim()
        };
    }
}
