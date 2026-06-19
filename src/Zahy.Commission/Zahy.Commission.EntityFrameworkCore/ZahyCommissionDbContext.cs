using Microsoft.EntityFrameworkCore;
using Volo.Abp.Data;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore.Modeling;
using Zahy.Identity.Partners;

namespace Zahy.Commission;

[ConnectionStringName("Default")]
public class ZahyCommissionDbContext : AbpDbContext<ZahyCommissionDbContext>
{
    public DbSet<CommissionRule> CommissionRules { get; set; }

    public DbSet<CommissionLedgerEntry> CommissionLedgerEntries { get; set; }

    public DbSet<BillingCharge> BillingCharges { get; set; }

    public DbSet<PartnerBillingProfile> PartnerBillingProfiles { get; set; }

    public ZahyCommissionDbContext(DbContextOptions<ZahyCommissionDbContext> options)
        : base(options)
    {
    }

    protected virtual bool IsPartnerFilterEnabled => DataFilter.IsEnabled<ICommissionPartnerDataFilter>();

    protected virtual Guid? CurrentPartnerId =>
        LazyServiceProvider.LazyGetService<ICurrentPartner>()?.Id;

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<CommissionRule>(b =>
        {
            b.ToTable("ComRules");
            b.ConfigureByConvention();

            b.Property(x => x.Name).IsRequired().HasMaxLength(CommissionConsts.MaxRuleNameLength);
            b.Property(x => x.BasisDefinitionJson).IsRequired();
            b.Property(x => x.ScopeCategoryCode).HasMaxLength(CommissionConsts.MaxScopeCodeLength);
            b.Property(x => x.ScopeProductSku).HasMaxLength(CommissionConsts.MaxScopeCodeLength);
            b.Property(x => x.IsEnabled).IsRequired();
            b.Property(x => x.Direction).IsRequired();
            b.Property(x => x.TriggerType).IsRequired();
            b.Property(x => x.FeeType).IsRequired();
            b.Property(x => x.BasisAmountKind).IsRequired();
            b.Property(x => x.ScopeKind).IsRequired();
            b.Property(x => x.EffectiveFromUtc).IsRequired();

            b.HasIndex(x => new { x.ScopeKind, x.ScopePartnerId, x.FeeType, x.IsEnabled });
            b.HasIndex(x => new { x.ScopeKind, x.ScopePartnerType, x.FeeType, x.IsEnabled });
        });

        builder.Entity<CommissionLedgerEntry>(b =>
        {
            b.ToTable("ComLedgerEntries");
            b.ConfigureByConvention();

            b.Property(x => x.SourceType).IsRequired().HasMaxLength(CommissionConsts.MaxSourceTypeLength);
            b.Property(x => x.SourceId).IsRequired().HasMaxLength(CommissionConsts.MaxSourceIdLength);
            b.Property(x => x.Currency).IsRequired().HasMaxLength(3);
            b.Property(x => x.IdempotencyKey).IsRequired().HasMaxLength(CommissionConsts.MaxIdempotencyKeyLength);
            b.Property(x => x.BasisAmount).HasPrecision(18, 4);
            b.Property(x => x.ComputedCommission).HasPrecision(18, 2);
            b.Property(x => x.Direction).IsRequired();
            b.Property(x => x.EntryKind).IsRequired();
            b.Property(x => x.Status).IsRequired();

            b.HasIndex(x => x.IdempotencyKey).IsUnique();
            b.HasIndex(x => x.PartnerId);
            b.HasIndex(x => new { x.SourceType, x.SourceId, x.RuleId });
            b.HasIndex(x => x.ReversesEntryId);

            b.HasQueryFilter(x =>
                !IsPartnerFilterEnabled ||
                CurrentPartnerId == null ||
                x.PartnerId == CurrentPartnerId);
        });

        builder.Entity<BillingCharge>(b =>
        {
            b.ToTable("ComBillingCharges");
            b.ConfigureByConvention();

            b.Property(x => x.Currency).IsRequired().HasMaxLength(3);
            b.Property(x => x.IdempotencyKey).IsRequired().HasMaxLength(BillingConsts.MaxIdempotencyKeyLength);
            b.Property(x => x.PeriodKey).HasMaxLength(BillingConsts.MaxPeriodKeyLength);
            b.Property(x => x.Description).HasMaxLength(BillingConsts.MaxDescriptionLength);
            b.Property(x => x.Amount).HasPrecision(18, 2);
            b.Property(x => x.Kind).IsRequired();
            b.Property(x => x.ChargedAt).IsRequired();

            b.HasIndex(x => x.IdempotencyKey).IsUnique();
            b.HasIndex(x => x.PartnerId);
            b.HasIndex(x => x.CommissionLedgerEntryId);

            b.HasQueryFilter(x =>
                !IsPartnerFilterEnabled ||
                CurrentPartnerId == null ||
                x.PartnerId == CurrentPartnerId);
        });

        builder.Entity<PartnerBillingProfile>(b =>
        {
            b.ToTable("ComPartnerBillingProfiles");
            b.ConfigureByConvention();

            b.Property(x => x.Currency).IsRequired().HasMaxLength(3);
            b.Property(x => x.ActivationFeeAmount).HasPrecision(18, 2);
            b.Property(x => x.MonthlySubscriptionAmount).HasPrecision(18, 2);
            b.Property(x => x.IsActive).IsRequired();

            b.HasIndex(x => x.PartnerId).IsUnique();
        });
    }
}
