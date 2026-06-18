using Microsoft.EntityFrameworkCore;
using Volo.Abp.Data;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore.Modeling;
using Zahy.Identity.Partners;
using Zahy.PartnerPlatform.Partners;

namespace Zahy.PartnerPlatform;

[ConnectionStringName("Default")]
public class ZahyPartnerPlatformDbContext : AbpDbContext<ZahyPartnerPlatformDbContext>
{
    public DbSet<Partner> Partners { get; set; }

    public DbSet<PartnerUser> PartnerUsers { get; set; }

    public ZahyPartnerPlatformDbContext(DbContextOptions<ZahyPartnerPlatformDbContext> options)
        : base(options)
    {
    }

    protected virtual bool IsPartnerFilterEnabled => DataFilter.IsEnabled<IPartnerDataFilter>();

    protected virtual Guid? CurrentPartnerId =>
        LazyServiceProvider.LazyGetService<ICurrentPartner>()?.Id;

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Partner>(b =>
        {
            b.ToTable("ZahyPartners");
            b.ConfigureByConvention();

            b.Property(x => x.LegalName).IsRequired().HasMaxLength(PartnerPlatformConsts.MaxLegalNameLength);
            b.Property(x => x.TradeName).HasMaxLength(PartnerPlatformConsts.MaxTradeNameLength);
            b.Property(x => x.PrimaryContactEmail).IsRequired().HasMaxLength(PartnerPlatformConsts.MaxEmailLength);
            b.Property(x => x.RegistrantName).IsRequired().HasMaxLength(PartnerPlatformConsts.MaxNameLength);
            b.Property(x => x.RegistrantPhone).IsRequired().HasMaxLength(PartnerPlatformConsts.MaxPhoneLength);
            b.Property(x => x.CloseNotes).HasMaxLength(PartnerPlatformConsts.MaxCloseNotesLength);
            b.Property(x => x.OpenIddictClientId).HasMaxLength(PartnerPlatformConsts.MaxOpenIddictClientIdLength);
            b.Property(x => x.Type).IsRequired();
            b.Property(x => x.Status).IsRequired();

            b.OwnsOne(x => x.ContactInfo, contact =>
            {
                contact.Property(x => x.ContactName).IsRequired().HasMaxLength(PartnerPlatformConsts.MaxNameLength);
                contact.Property(x => x.Email).IsRequired().HasMaxLength(PartnerPlatformConsts.MaxEmailLength);
                contact.Property(x => x.Phone).IsRequired().HasMaxLength(PartnerPlatformConsts.MaxPhoneLength);
                contact.Property(x => x.AddressLine1).IsRequired().HasMaxLength(PartnerPlatformConsts.MaxAddressLineLength);
                contact.Property(x => x.AddressLine2).HasMaxLength(PartnerPlatformConsts.MaxAddressLineLength);
                contact.Property(x => x.City).IsRequired().HasMaxLength(PartnerPlatformConsts.MaxCityLength);
                contact.Property(x => x.Region).IsRequired().HasMaxLength(PartnerPlatformConsts.MaxRegionLength);
                contact.Property(x => x.PostalCode).HasMaxLength(PartnerPlatformConsts.MaxPostalCodeLength);
                contact.Property(x => x.CountryCode).IsRequired().HasMaxLength(PartnerPlatformConsts.MaxCountryCodeLength);
            });

            b.OwnsOne(x => x.BankInfo, bank =>
            {
                bank.Property(x => x.BankName).HasMaxLength(PartnerPlatformConsts.MaxBankNameLength);
                bank.Property(x => x.AccountHolderName).HasMaxLength(PartnerPlatformConsts.MaxAccountHolderNameLength);
                bank.Property(x => x.Iban).HasMaxLength(PartnerPlatformConsts.MaxIbanLength);
                bank.Property(x => x.SwiftCode).HasMaxLength(PartnerPlatformConsts.MaxSwiftCodeLength);
            });

            b.HasIndex(x => x.Status);
            b.HasIndex(x => x.PrimaryContactEmail);
            b.HasIndex(x => new { x.Status, x.PrimaryContactEmail });
        });

        builder.Entity<PartnerUser>(b =>
        {
            b.ToTable("ZahyPartnerUsers");
            b.ConfigureByConvention();

            b.Property(x => x.Role).IsRequired().HasMaxLength(128);
            b.Property(x => x.Status).IsRequired();

            b.HasIndex(x => x.PartnerId);
            b.HasIndex(x => new { x.PartnerId, x.IdentityUserId }).IsUnique();

            b.HasQueryFilter(u =>
                !IsPartnerFilterEnabled ||
                (CurrentPartnerId != null && u.PartnerId == CurrentPartnerId));
        });
    }
}
