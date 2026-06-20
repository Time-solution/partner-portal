using Microsoft.EntityFrameworkCore;
using Volo.Abp.Data;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore.Modeling;
using Zahy.Identity.Partners;

namespace Zahy.PartnerCatalog;

[ConnectionStringName("Default")]
public class ZahyPartnerCatalogDbContext : AbpDbContext<ZahyPartnerCatalogDbContext>
{
    public DbSet<PartnerCatalogItem> PartnerCatalogItems => Set<PartnerCatalogItem>();

    public DbSet<PartnerCatalogItemReflection> PartnerCatalogItemReflections => Set<PartnerCatalogItemReflection>();

    public DbSet<MerchantActivation> MerchantActivations => Set<MerchantActivation>();

    public DbSet<SettlementCostMarkupSnapshot> SettlementCostMarkupSnapshots =>
        Set<SettlementCostMarkupSnapshot>();

    public DbSet<PlatformCatalogLink> PlatformCatalogLinks => Set<PlatformCatalogLink>();

    public DbSet<ReflectedPartnerOrder> ReflectedPartnerOrders => Set<ReflectedPartnerOrder>();

    public ZahyPartnerCatalogDbContext(DbContextOptions<ZahyPartnerCatalogDbContext> options)
        : base(options)
    {
    }

    protected virtual bool IsPartnerFilterEnabled => DataFilter.IsEnabled<IPartnerCatalogDataFilter>();

    protected virtual Guid? CurrentPartnerId =>
        LazyServiceProvider.LazyGetService<ICurrentPartner>()?.Id;

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<PartnerCatalogItem>(b =>
        {
            b.ToTable("PcatPartnerCatalogItems");
            b.ConfigureByConvention();

            b.Property(x => x.Code).IsRequired().HasMaxLength(PartnerCatalogConsts.MaxCodeLength);
            b.Property(x => x.Name).IsRequired().HasMaxLength(PartnerCatalogConsts.MaxNameLength);
            b.Property(x => x.Description).HasMaxLength(PartnerCatalogConsts.MaxDescriptionLength);
            b.Property(x => x.OfferingKind).IsRequired();
            b.Property(x => x.PartnerCostAmount).HasPrecision(18, 2);
            b.Property(x => x.PartnerCostCurrency).IsRequired().HasMaxLength(3);
            b.Property(x => x.PartnerCostVatInclusive).IsRequired();
            b.Property(x => x.Status).IsRequired();
            b.Property(x => x.SettlementTriggerMode).IsRequired();
            b.Property(x => x.DefaultVatTreatment).IsRequired();
            b.Property(x => x.CarrierServiceCode).HasMaxLength(PartnerCatalogConsts.MaxCarrierServiceCodeLength);
            b.Property(x => x.ExternalMenuItemId).HasMaxLength(PartnerCatalogConsts.MaxExternalMenuItemIdLength);
            b.Property(x => x.MenuCategoryCode).HasMaxLength(PartnerCatalogConsts.MaxMenuCategoryCodeLength);
            b.Property(x => x.SettlementParticipationMode).IsRequired();
            b.Property(x => x.ConsignmentOwnershipMode);

            b.HasIndex(x => x.PartnerId);
            b.HasIndex(x => new { x.PartnerId, x.Code }).IsUnique();
            b.HasIndex(x => new { x.PartnerId, x.Status });

            b.HasQueryFilter(x =>
                !IsPartnerFilterEnabled ||
                CurrentPartnerId == null ||
                x.PartnerId == CurrentPartnerId);
        });

        builder.Entity<PartnerCatalogItemReflection>(b =>
        {
            b.ToTable("PcatPartnerCatalogItemReflections");
            b.ConfigureByConvention();

            b.Property(x => x.Audience).IsRequired();
            b.Property(x => x.VisibleFrom).IsRequired();

            b.HasIndex(x => x.PartnerCatalogItemId);
            b.HasIndex(x => new { x.PartnerId, x.IsPublished, x.VisibleFrom });

            b.HasQueryFilter(x =>
                !IsPartnerFilterEnabled ||
                CurrentPartnerId == null ||
                x.PartnerId == CurrentPartnerId);
        });

        builder.Entity<MerchantActivation>(b =>
        {
            b.ToTable("PcatMerchantActivations");
            b.ConfigureByConvention();

            b.Property(x => x.ResalePriceAmount).HasPrecision(18, 2);
            b.Property(x => x.ResalePriceCurrency).IsRequired().HasMaxLength(3);
            b.Property(x => x.ResalePriceVatInclusive).IsRequired();
            b.Property(x => x.Status).IsRequired();
            b.Property(x => x.IdempotencyKey).IsRequired().HasMaxLength(PartnerCatalogConsts.MaxIdempotencyKeyLength);
            b.Property(x => x.ExternalReference).HasMaxLength(PartnerCatalogConsts.MaxExternalReferenceLength);

            b.HasIndex(x => x.IdempotencyKey).IsUnique();
            b.HasIndex(x => x.PartnerId);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => new { x.TenantId, x.PartnerCatalogItemId });

            b.HasQueryFilter(x =>
                !IsPartnerFilterEnabled ||
                CurrentPartnerId == null ||
                x.PartnerId == CurrentPartnerId);
        });

        builder.Entity<SettlementCostMarkupSnapshot>(b =>
        {
            b.ToTable("PcatSettlementCostMarkupSnapshots");
            b.ConfigureByConvention();

            b.Property(x => x.BuyPriceAmount).HasPrecision(18, 2);
            b.Property(x => x.BuyPriceCurrency).IsRequired().HasMaxLength(3);
            b.Property(x => x.BuyPriceVatInclusive).IsRequired();
            b.Property(x => x.SellPriceAmount).HasPrecision(18, 2);
            b.Property(x => x.SellPriceCurrency).IsRequired().HasMaxLength(3);
            b.Property(x => x.SellPriceVatInclusive).IsRequired();
            b.Property(x => x.SellPriceSource).IsRequired();
            b.Property(x => x.SettlementBook).IsRequired();
            b.Property(x => x.VatTreatment).IsRequired();
            b.Property(x => x.Trigger).IsRequired();
            b.Property(x => x.ExternalTransactionId)
                .IsRequired()
                .HasMaxLength(PartnerCatalogConsts.MaxExternalTransactionIdLength);
            b.Property(x => x.OrderLineId)
                .IsRequired()
                .HasMaxLength(PartnerCatalogConsts.MaxOrderLineIdLength)
                .HasDefaultValue(string.Empty);
            b.Property(x => x.CreatedAt).IsRequired();
            b.Property(x => x.BillingChargeId);

            b.HasIndex(x => new { x.ExternalTransactionId, x.OrderLineId }).IsUnique();
            b.HasIndex(x => x.MerchantActivationId);
            b.HasIndex(x => x.PartnerId);
            b.HasIndex(x => x.TenantId);

            b.HasQueryFilter(x =>
                !IsPartnerFilterEnabled ||
                CurrentPartnerId == null ||
                x.PartnerId == CurrentPartnerId);
        });

        builder.Entity<PlatformCatalogLink>(b =>
        {
            b.ToTable("PcatPlatformCatalogLinks");
            b.ConfigureByConvention();

            b.Property(x => x.Status).IsRequired();
            b.Property(x => x.LastSyncError).HasMaxLength(PartnerCatalogConsts.MaxSyncErrorLength);
            b.Property(x => x.Shape2HandshakeVersion)
                .HasMaxLength(PartnerCatalogConsts.MaxShape2HandshakeVersionLength);

            b.HasIndex(x => x.PartnerCatalogItemId);
            b.HasIndex(x => x.TenantId);
        });

        builder.Entity<ReflectedPartnerOrder>(b =>
        {
            b.ToTable("PcatReflectedPartnerOrders");
            b.ConfigureByConvention();

            b.Property(x => x.ExternalTransactionId)
                .IsRequired()
                .HasMaxLength(PartnerCatalogConsts.MaxExternalTransactionIdLength);
            b.Property(x => x.OrderLineId)
                .IsRequired()
                .HasMaxLength(PartnerCatalogConsts.MaxOrderLineIdLength)
                .HasDefaultValue(string.Empty);
            b.Property(x => x.ReflectedAt).IsRequired();

            b.HasIndex(x => new { x.ExternalTransactionId, x.OrderLineId }).IsUnique();
            b.HasIndex(x => x.MerchantActivationId);
            b.HasIndex(x => x.PartnerId);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => x.SettlementCostMarkupSnapshotId);

            b.HasQueryFilter(x =>
                !IsPartnerFilterEnabled ||
                CurrentPartnerId == null ||
                x.PartnerId == CurrentPartnerId);
        });
    }
}
