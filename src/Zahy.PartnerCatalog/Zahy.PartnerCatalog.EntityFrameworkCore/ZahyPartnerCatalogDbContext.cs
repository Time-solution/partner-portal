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

    public DbSet<UsagePackage> UsagePackages => Set<UsagePackage>();

    public DbSet<UsagePackageSelection> UsagePackageSelections => Set<UsagePackageSelection>();

    public DbSet<PartnerCatalogProfile> PartnerCatalogProfiles => Set<PartnerCatalogProfile>();

    public DbSet<PartnerCatalogListing> PartnerCatalogListings => Set<PartnerCatalogListing>();

    public DbSet<ServiceOrder> ServiceOrders => Set<ServiceOrder>();

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
            b.Property(x => x.MerchantBenefit).HasMaxLength(PartnerCatalogConsts.MaxMerchantBenefitLength);
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

        builder.Entity<UsagePackage>(b =>
        {
            b.ToTable("PcatUsagePackages");
            b.ConfigureByConvention();

            b.Property(x => x.PartnerId).IsRequired();
            b.Property(x => x.Name).IsRequired().HasMaxLength(UsagePackageConsts.MaxNameLength);
            b.Property(x => x.UnitLabel).IsRequired().HasMaxLength(UsagePackageConsts.MaxUnitLabelLength);
            b.Property(x => x.Mode).IsRequired();
            b.Property(x => x.Currency).IsRequired().HasMaxLength(3);
            b.Property(x => x.IncludedQuantity).HasPrecision(18, 2);
            b.Property(x => x.BaseBuyAmount).HasPrecision(18, 2);
            b.Property(x => x.BaseSellAmount).HasPrecision(18, 2);
            b.Property(x => x.OverageBuyAmount).HasPrecision(18, 2);
            b.Property(x => x.OverageSellAmount).HasPrecision(18, 2);
            b.Property(x => x.Payer).IsRequired();
            b.Property(x => x.Status).IsRequired();
            b.Property(x => x.PackageExplanation).HasMaxLength(PartnerCatalogConsts.MaxPackageExplanationLength);
            // U5 — optional volume tiers stored as JSON (null = flat overage; provider-agnostic). The
            // deserialized Tiers collection is computed, not a navigation — map only the JSON column.
            b.Property(x => x.TiersJson);
            b.Ignore(x => x.Tiers);

            // Multiple packages per partner are allowed — index, NOT unique.
            b.HasIndex(x => x.PartnerId);
            b.HasIndex(x => new { x.PartnerId, x.Status });

            b.HasQueryFilter(x =>
                !IsPartnerFilterEnabled ||
                CurrentPartnerId == null ||
                x.PartnerId == CurrentPartnerId);
        });

        builder.Entity<UsagePackageSelection>(b =>
        {
            b.ToTable("PcatUsagePackageSelections");
            b.ConfigureByConvention();

            b.Property(x => x.PartnerId).IsRequired();
            b.Property(x => x.TenantId).IsRequired();
            b.Property(x => x.UsagePackageId).IsRequired();
            b.Property(x => x.MerchantName).HasMaxLength(PartnerCatalogConsts.MaxNameLength);
            b.Property(x => x.Status).IsRequired();
            b.Property(x => x.ActivatedAt).IsRequired();

            b.HasIndex(x => x.PartnerId);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => new { x.TenantId, x.PartnerId });
            b.HasIndex(x => new { x.TenantId, x.UsagePackageId });

            b.HasQueryFilter(x =>
                !IsPartnerFilterEnabled ||
                CurrentPartnerId == null ||
                x.PartnerId == CurrentPartnerId);
        });

        builder.Entity<PartnerCatalogProfile>(b =>
        {
            b.ToTable("PcatPartnerProfiles");
            b.ConfigureByConvention();

            b.Property(x => x.PartnerId).IsRequired();
            b.Property(x => x.PartnerBrief).HasMaxLength(PartnerCatalogConsts.MaxPartnerBriefLength);

            // One presentation profile per partner.
            b.HasIndex(x => x.PartnerId).IsUnique();

            b.HasQueryFilter(x =>
                !IsPartnerFilterEnabled ||
                CurrentPartnerId == null ||
                x.PartnerId == CurrentPartnerId);
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

        builder.Entity<PartnerCatalogListing>(b =>
        {
            b.ToTable("PcatListings");
            b.ConfigureByConvention();

            b.Property(x => x.PartnerCatalogItemId).IsRequired();
            b.Property(x => x.PartnerId).IsRequired();

            // One structured listing per offering.
            b.HasIndex(x => x.PartnerCatalogItemId).IsUnique();

            // Owned rows: EXPLICIT Guid keys, app-managed OrderIndex — never DB IDENTITY sequence
            // (the LineNo lesson). Whole-document replace reconciles as delete+insert by key.
            b.OwnsMany(x => x.Requirements, r =>
            {
                r.ToTable("PcatListingRequirements");
                r.WithOwner().HasForeignKey("ListingId");
                r.HasKey(x => x.Id);
                r.Property(x => x.Id).ValueGeneratedNever();
                r.Property(x => x.OrderIndex).IsRequired();
                r.Property(x => x.Title).IsRequired().HasMaxLength(PartnerCatalogListingConsts.MaxRequirementTitleLength);
                r.Property(x => x.Type).IsRequired();
                r.Property(x => x.ChoicesJson).IsRequired().HasMaxLength(1024);
                r.Ignore(x => x.Choices);
            });

            b.OwnsMany(x => x.Deliverables, d =>
            {
                d.ToTable("PcatListingDeliverables");
                d.WithOwner().HasForeignKey("ListingId");
                d.HasKey(x => x.Id);
                d.Property(x => x.Id).ValueGeneratedNever();
                d.Property(x => x.OrderIndex).IsRequired();
                d.Property(x => x.Title).IsRequired().HasMaxLength(PartnerCatalogListingConsts.MaxDeliverableTitleLength);
                d.Property(x => x.Quantity).IsRequired();
            });

            b.OwnsMany(x => x.ExecutionSteps, e =>
            {
                e.ToTable("PcatListingExecutionSteps");
                e.WithOwner().HasForeignKey("ListingId");
                e.HasKey(x => x.Id);
                e.Property(x => x.Id).ValueGeneratedNever();
                e.Property(x => x.OrderIndex).IsRequired();
                e.Property(x => x.Text).IsRequired().HasMaxLength(PartnerCatalogListingConsts.MaxExecutionStepLength);
            });

            b.OwnsMany(x => x.Terms, t =>
            {
                t.ToTable("PcatListingTerms");
                t.WithOwner().HasForeignKey("ListingId");
                t.HasKey(x => x.Id);
                t.Property(x => x.Id).ValueGeneratedNever();
                t.Property(x => x.OrderIndex).IsRequired();
                t.Property(x => x.Text).IsRequired().HasMaxLength(PartnerCatalogListingConsts.MaxTermLength);
            });

            b.OwnsMany(x => x.Faqs, f =>
            {
                f.ToTable("PcatListingFaqs");
                f.WithOwner().HasForeignKey("ListingId");
                f.HasKey(x => x.Id);
                f.Property(x => x.Id).ValueGeneratedNever();
                f.Property(x => x.OrderIndex).IsRequired();
                f.Property(x => x.Question).IsRequired().HasMaxLength(PartnerCatalogListingConsts.MaxFaqQuestionLength);
                f.Property(x => x.Answer).IsRequired().HasMaxLength(PartnerCatalogListingConsts.MaxFaqAnswerLength);
            });

            b.Ignore(x => x.IsEmpty);

            b.HasQueryFilter(x =>
                !IsPartnerFilterEnabled ||
                CurrentPartnerId == null ||
                x.PartnerId == CurrentPartnerId);
        });

        // The ABP base model walk discovers these row types as NON-owned before this block runs;
        // Ignore removes that registration so OwnsMany below re-adds them as owned (the remedy the
        // EF error itself prescribes — same family as the SettlementStateTransition ignore above).
        builder.Ignore<ServiceOrderAnswer>();
        builder.Ignore<ServiceOrderMilestone>();
        builder.Ignore<ServiceOrderHistoryEntry>();

        builder.Entity<ServiceOrder>(b =>
        {
            b.ToTable("PcatServiceOrders");
            b.ConfigureByConvention();

            b.Property(x => x.PartnerId).IsRequired();
            b.Property(x => x.PartnerCatalogItemId).IsRequired();
            b.Property(x => x.OfferingNameSnapshot).IsRequired().HasMaxLength(PartnerCatalogConsts.MaxNameLength);
            b.Property(x => x.Status).IsRequired();
            b.Property(x => x.ParticipationModeSnapshot).IsRequired();
            b.Property(x => x.BuySnapshotAmount).HasColumnType("decimal(18,2)");
            b.Property(x => x.SellSnapshotAmount).HasColumnType("decimal(18,2)");
            b.Property(x => x.FeeSnapshotAmount).HasColumnType("decimal(18,2)");
            b.Property(x => x.Currency).IsRequired().HasMaxLength(3);

            b.HasIndex(x => x.PartnerId);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => new { x.TenantId, x.PartnerCatalogItemId });
            b.HasIndex(x => x.Status);

            // Owned rows: explicit client Guids (ValueGeneratedNever), app-managed OrderIndex —
            // the 2a pattern; never DB IDENTITY sequence.
            b.OwnsMany(x => x.Answers, a =>
            {
                a.ToTable("PcatServiceOrderAnswers");
                a.WithOwner().HasForeignKey("ServiceOrderId");
                a.HasKey(x => x.Id);
                a.Property(x => x.Id).ValueGeneratedNever();
                a.Property(x => x.OrderIndex).IsRequired();
                a.Property(x => x.RequirementTitleSnapshot).IsRequired()
                    .HasMaxLength(PartnerCatalogListingConsts.MaxRequirementTitleLength);
                a.Property(x => x.RequirementType).IsRequired();
                a.Property(x => x.AnswerText).IsRequired()
                    .HasMaxLength(PartnerCatalogServiceOrderConsts.MaxLongTextAnswerLength);
            });

            b.OwnsMany(x => x.Milestones, m =>
            {
                m.ToTable("PcatServiceOrderMilestones");
                m.WithOwner().HasForeignKey("ServiceOrderId");
                m.HasKey(x => x.Id);
                m.Property(x => x.Id).ValueGeneratedNever();
                m.Property(x => x.OrderIndex).IsRequired();
                m.Property(x => x.Title).IsRequired()
                    .HasMaxLength(PartnerCatalogServiceOrderConsts.MaxMilestoneTitleLength);
                m.Property(x => x.Amount).IsRequired().HasColumnType("decimal(18,2)");
            });

            b.OwnsMany(x => x.History, h =>
            {
                h.ToTable("PcatServiceOrderHistory");
                h.WithOwner().HasForeignKey("ServiceOrderId");
                h.HasKey(x => x.Id);
                h.Property(x => x.Id).ValueGeneratedNever();
                h.Property(x => x.OrderIndex).IsRequired();
                h.Property(x => x.Action).IsRequired();
                h.Property(x => x.ToStatus).IsRequired();
                h.Property(x => x.Actor).IsRequired().HasMaxLength(PartnerCatalogServiceOrderConsts.MaxActorLength);
                h.Property(x => x.Note).HasMaxLength(PartnerCatalogServiceOrderConsts.MaxNoteLength);
                h.Property(x => x.At).IsRequired();
            });

            b.Ignore(x => x.MerchantPriceAmount);
            b.Ignore(x => x.IsTerminal);

            // Partner scoping (merchant scoping comes from the built-in IMultiTenant filter):
            // cross-partner access is structurally invisible (the ratified 2a convention).
            b.HasQueryFilter(x =>
                !IsPartnerFilterEnabled ||
                CurrentPartnerId == null ||
                x.PartnerId == CurrentPartnerId);
        });
    }
}
