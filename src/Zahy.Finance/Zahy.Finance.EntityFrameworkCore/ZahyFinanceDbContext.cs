using Microsoft.EntityFrameworkCore;
using Volo.Abp.Data;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore.Modeling;
using Volo.Abp.MultiTenancy;
using Zahy.Identity.Partners;

namespace Zahy.Finance;

[ConnectionStringName("Default")]
public class ZahyFinanceDbContext : AbpDbContext<ZahyFinanceDbContext>
{
    public DbSet<PartnerFinancialAccount> PartnerFinancialAccounts { get; set; }

    public DbSet<MerchantAccount> MerchantAccounts { get; set; }

    public DbSet<AccountPosting> AccountPostings { get; set; }

    public DbSet<KycSubmission> KycSubmissions { get; set; }

    public DbSet<KycVerification> KycVerifications { get; set; }

    public ZahyFinanceDbContext(DbContextOptions<ZahyFinanceDbContext> options)
        : base(options)
    {
    }

    protected virtual bool IsPartnerFilterEnabled => DataFilter.IsEnabled<IFinancePartnerDataFilter>();

    protected virtual bool IsTenantFilterEnabled => DataFilter.IsEnabled<IFinanceTenantDataFilter>();

    protected virtual Guid? CurrentPartnerId =>
        LazyServiceProvider.LazyGetService<ICurrentPartner>()?.Id;

    protected virtual Guid? FinanceCurrentTenantId =>
        LazyServiceProvider.LazyGetService<ICurrentTenant>()?.Id;

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<PartnerFinancialAccount>(b =>
        {
            b.ToTable("FinPartnerAccounts");
            b.ConfigureByConvention();

            b.Property(x => x.Status).IsRequired();
            b.Property(x => x.OpenedAt).IsRequired();
            b.Property(x => x.KycVerificationId).IsRequired();

            b.HasIndex(x => x.PartnerId).IsUnique();

            b.HasQueryFilter(x =>
                !IsPartnerFilterEnabled ||
                CurrentPartnerId == null ||
                x.PartnerId == CurrentPartnerId);
        });

        builder.Entity<MerchantAccount>(b =>
        {
            b.ToTable("FinMerchantAccounts");
            b.ConfigureByConvention();

            b.Property(x => x.Status).IsRequired();
            b.Property(x => x.OpenedAt).IsRequired();
            b.Property(x => x.KycVerificationId).IsRequired();

            b.HasIndex(x => x.TenantId).IsUnique();

            b.HasQueryFilter(x =>
                !IsTenantFilterEnabled ||
                CurrentTenantId == null ||
                x.TenantId == FinanceCurrentTenantId);
        });

        builder.Entity<AccountPosting>(b =>
        {
            b.ToTable("FinAccountPostings");
            b.ConfigureByConvention();

            b.Property(x => x.SourceType).IsRequired().HasMaxLength(FinanceConsts.MaxSourceTypeLength);
            b.Property(x => x.SourceId).IsRequired().HasMaxLength(FinanceConsts.MaxSourceIdLength);
            b.Property(x => x.Currency).IsRequired().HasMaxLength(3);
            b.Property(x => x.IdempotencyKey).IsRequired().HasMaxLength(FinanceConsts.MaxIdempotencyKeyLength);
            b.Property(x => x.Description).HasMaxLength(FinanceConsts.MaxDescriptionLength);
            b.Property(x => x.PostingAmount).HasPrecision(18, 2);
            b.Property(x => x.AccountKind).IsRequired();
            b.Property(x => x.SourceModule).IsRequired();
            b.Property(x => x.PostedAt).IsRequired();

            b.HasIndex(x => x.IdempotencyKey).IsUnique();
            b.HasIndex(x => new { x.AccountKind, x.AccountId });

            b.HasQueryFilter(x =>
                (!IsPartnerFilterEnabled || CurrentPartnerId == null || x.PartnerId == CurrentPartnerId) &&
                (!IsTenantFilterEnabled || FinanceCurrentTenantId == null || x.TenantId == null || x.TenantId == FinanceCurrentTenantId));
        });

        builder.Entity<KycSubmission>(b =>
        {
            b.ToTable("FinKycSubmissions");
            b.ConfigureByConvention();

            b.Property(x => x.EntityKind).IsRequired();
            b.Property(x => x.SubmittedAt).IsRequired();

            b.HasIndex(x => new { x.EntityKind, x.EntityId });
        });

        builder.Entity<KycVerification>(b =>
        {
            b.ToTable("FinKycVerifications");
            b.ConfigureByConvention();

            b.Property(x => x.EntityKind).IsRequired();
            b.Property(x => x.Status).IsRequired();

            b.HasIndex(x => new { x.EntityKind, x.EntityId, x.Status });
        });
    }
}
