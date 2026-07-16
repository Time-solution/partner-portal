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

    public DbSet<FinancePlatformSettings> FinancePlatformSettings { get; set; }

    public DbSet<FinanceDocument> FinanceDocuments { get; set; }

    public DbSet<InvoiceNumberSequence> InvoiceNumberSequences { get; set; }

    public DbSet<FinanceAccountStatusAudit> FinanceAccountStatusAudits { get; set; }

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
                (!IsPartnerFilterEnabled ||
                 CurrentPartnerId == null ||
                 x.AccountKind != FinanceAccountKind.Partner ||
                 x.PartnerId == CurrentPartnerId) &&
                (!IsTenantFilterEnabled ||
                 FinanceCurrentTenantId == null ||
                 x.AccountKind != FinanceAccountKind.Merchant ||
                 x.TenantId == FinanceCurrentTenantId));
        });

        builder.Entity<KycSubmission>(b =>
        {
            b.ToTable("FinKycSubmissions");
            b.ConfigureByConvention();

            b.Property(x => x.EntityKind).IsRequired();
            b.Property(x => x.Version).IsRequired();
            b.Property(x => x.SubmittedAt).IsRequired();
            b.Property(x => x.ProtectedLegalNameAr).IsRequired().HasMaxLength(FinanceKycConsts.MaxProtectedFieldLength);
            b.Property(x => x.ProtectedLegalNameEn).IsRequired().HasMaxLength(FinanceKycConsts.MaxProtectedFieldLength);
            b.Property(x => x.ProtectedCommercialRegistrationNumber).IsRequired().HasMaxLength(FinanceKycConsts.MaxProtectedFieldLength);
            b.Property(x => x.ProtectedVatNumber).IsRequired().HasMaxLength(FinanceKycConsts.MaxProtectedFieldLength);
            b.Property(x => x.ProtectedIban).IsRequired().HasMaxLength(FinanceKycConsts.MaxProtectedFieldLength);
            b.Property(x => x.ProtectedLegalAddress).IsRequired().HasMaxLength(FinanceKycConsts.MaxProtectedFieldLength);

            b.HasIndex(x => new { x.EntityKind, x.EntityId, x.Version }).IsUnique();
            b.HasIndex(x => new { x.EntityKind, x.EntityId });
        });

        builder.Entity<KycVerification>(b =>
        {
            b.ToTable("FinKycVerifications");
            b.ConfigureByConvention();

            b.Property(x => x.EntityKind).IsRequired();
            b.Property(x => x.Status).IsRequired();
            b.Property(x => x.ReviewNotes).HasMaxLength(FinanceKycConsts.MaxReviewNotesLength);
            b.Property(x => x.VerifiedLegalNameAr).HasMaxLength(FinanceKycConsts.MaxLegalNameLength);
            b.Property(x => x.VerifiedLegalNameEn).HasMaxLength(FinanceKycConsts.MaxLegalNameLength);
            b.Property(x => x.VerifiedCommercialRegistrationNumber).HasMaxLength(FinanceKycConsts.MaxCrNumberLength);
            b.Property(x => x.VerifiedVatNumber).HasMaxLength(FinanceKycConsts.MaxVatNumberLength);
            b.Property(x => x.VerifiedIban).HasMaxLength(FinanceKycConsts.MaxIbanLength);
            b.Property(x => x.VerifiedLegalAddress).HasMaxLength(FinanceKycConsts.MaxAddressLength);

            b.HasIndex(x => new { x.EntityKind, x.EntityId, x.Status });
            b.HasIndex(x => x.KycSubmissionId).IsUnique();
        });

        builder.Entity<FinancePlatformSettings>(b =>
        {
            b.ToTable("FinPlatformSettings");
            b.ConfigureByConvention();
            b.Property(x => x.DefaultInvoiceGenerationMode).IsRequired();
        });

        builder.Entity<FinanceDocument>(b =>
        {
            b.ToTable("FinDocuments");
            b.ConfigureByConvention();

            b.Property(x => x.IdempotencyKey).IsRequired().HasMaxLength(FinanceConsts.MaxIdempotencyKeyLength);
            b.Property(x => x.InvoiceNumber).IsRequired().HasMaxLength(64);
            b.Property(x => x.Currency).IsRequired().HasMaxLength(3);
            b.Property(x => x.PostingSum).HasPrecision(18, 2);
            b.Property(x => x.DocumentKind).IsRequired();
            b.Property(x => x.GeneratedAt).IsRequired();

            // Manual (ad-hoc) invoice columns — nullable / defaulted, so existing LedgerDerived rows are unaffected.
            b.Property(x => x.Source).IsRequired().HasDefaultValue(FinanceDocumentSource.LedgerDerived);
            // P4 lifecycle — plain ADD COLUMN NOT NULL DEFAULT: every pre-existing row backfills to
            // Issued (generated documents are final); only newly-created manual drafts carry Draft.
            b.Property(x => x.Status).IsRequired().HasDefaultValue(FinanceDocumentStatus.Issued);
            b.Property(x => x.Recipient).HasMaxLength(FinanceConsts.MaxRecipientLength);
            b.Property(x => x.RecipientType);
            b.Property(x => x.RecipientReference);
            b.Property(x => x.Notes).HasMaxLength(FinanceConsts.MaxNotesLength);

            b.OwnsMany(x => x.Lines, line =>
            {
                line.ToTable("FinInvoiceLines");
                line.WithOwner().HasForeignKey("FinanceDocumentId");
                line.HasKey("FinanceDocumentId", "LineNo");

                line.Property(x => x.LineNo).IsRequired().ValueGeneratedNever();
                line.Property(x => x.Description).IsRequired().HasMaxLength(FinanceConsts.MaxLineDescriptionLength);
                line.Property(x => x.Quantity).HasPrecision(18, 4);
                line.Property(x => x.UnitPriceInclusive).HasPrecision(18, 2);
                line.Property(x => x.LineTotalInclusive).HasPrecision(18, 2);
                line.Property(x => x.VatNet).HasPrecision(18, 2);
                line.Property(x => x.VatAmount).HasPrecision(18, 2);
                line.Property(x => x.AccountCode).IsRequired().HasMaxLength(FinanceConsts.MaxAccountCodeLength);
            });
            b.Navigation(x => x.Lines).UsePropertyAccessMode(PropertyAccessMode.Field);

            b.HasIndex(x => x.IdempotencyKey).IsUnique();
            b.HasIndex(x => new { x.AccountKind, x.AccountId });
            // AF3 — atomic backstop for the P5 :085 duplicate-reference check (global on InvoiceNumber,
            // matching the gate's `x.InvoiceNumber == document.InvoiceNumber` scope; MAN- and ZAHY-INV
            // pools never collide by prefix). Non-nullable required column → plain unique, no filter.
            b.HasIndex(x => x.InvoiceNumber).IsUnique();

            b.HasQueryFilter(x =>
                (!IsPartnerFilterEnabled ||
                 CurrentPartnerId == null ||
                 (x.AccountKind == FinanceAccountKind.Partner && x.EntityId == CurrentPartnerId) ||
                 x.AccountKind != FinanceAccountKind.Partner) &&
                (!IsTenantFilterEnabled ||
                 FinanceCurrentTenantId == null ||
                 (x.AccountKind == FinanceAccountKind.Merchant && x.EntityId == FinanceCurrentTenantId) ||
                 x.AccountKind != FinanceAccountKind.Merchant));
        });

        builder.Entity<InvoiceNumberSequence>(b =>
        {
            b.ToTable("FinInvoiceNumberSequences");
            b.ConfigureByConvention();

            b.Property(x => x.DocumentKind).IsRequired();
            b.Property(x => x.FiscalYear).IsRequired();
            b.Property(x => x.LastNumber).IsRequired();

            b.HasIndex(x => new { x.DocumentKind, x.FiscalYear }).IsUnique();
        });

        builder.Entity<FinanceAccountStatusAudit>(b =>
        {
            b.ToTable("FinAccountStatusAudits");
            b.ConfigureByConvention();

            b.Property(x => x.Reason).HasMaxLength(FinanceConsts.MaxDescriptionLength);
            b.Property(x => x.TransitionedAt).IsRequired();

            b.HasIndex(x => new { x.AccountKind, x.AccountId });
        });
    }
}
