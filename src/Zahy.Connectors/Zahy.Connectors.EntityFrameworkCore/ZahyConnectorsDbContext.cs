using Microsoft.EntityFrameworkCore;
using Volo.Abp.Data;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore.Modeling;
using Zahy.Identity.Partners;

namespace Zahy.Connectors;

[ConnectionStringName("Default")]
public class ZahyConnectorsDbContext : AbpDbContext<ZahyConnectorsDbContext>
{
    public DbSet<ConnectorRegistration> ConnectorRegistrations { get; set; }

    public DbSet<ConnectorBranchMapping> ConnectorBranchMappings { get; set; }

    public ZahyConnectorsDbContext(DbContextOptions<ZahyConnectorsDbContext> options)
        : base(options)
    {
    }

    protected virtual bool IsPartnerFilterEnabled => DataFilter.IsEnabled<IConnectorPartnerDataFilter>();

    protected virtual Guid? CurrentPartnerId =>
        LazyServiceProvider.LazyGetService<ICurrentPartner>()?.Id;

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<ConnectorRegistration>(b =>
        {
            b.ToTable("ConnRegistrations");
            b.ConfigureByConvention();

            b.Property(x => x.ConnectorCode).IsRequired().HasMaxLength(ConnectorConsts.MaxConnectorCodeLength);
            b.Property(x => x.ConnectorKind).IsRequired();
            b.Property(x => x.DisplayName).IsRequired().HasMaxLength(ConnectorConsts.MaxDisplayNameLength);
            b.Property(x => x.ConfigJson).HasMaxLength(ConnectorConsts.MaxConfigJsonLength);
            b.Property(x => x.SecretReference).IsRequired().HasMaxLength(ConnectorConsts.MaxSecretReferenceLength);
            b.Property(x => x.IsEnabled).IsRequired();

            b.HasIndex(x => x.PartnerId);
            b.HasIndex(x => new { x.PartnerId, x.TenantId, x.ConnectorCode }).IsUnique();

            b.HasQueryFilter(x =>
                !IsPartnerFilterEnabled ||
                CurrentPartnerId == null ||
                x.PartnerId == CurrentPartnerId);
        });

        builder.Entity<ConnectorBranchMapping>(b =>
        {
            b.ToTable("ConnBranchMappings");
            b.ConfigureByConvention();

            b.Property(x => x.ConnectorCode).IsRequired().HasMaxLength(ConnectorConsts.MaxConnectorCodeLength);
            b.Property(x => x.ExternalOutletId).IsRequired().HasMaxLength(ConnectorConsts.MaxExternalIdLength);
            b.Property(x => x.IsActive).IsRequired();

            b.HasIndex(x => x.PartnerId);
            b.HasIndex(x => new
            {
                x.PartnerId,
                x.TenantId,
                x.ConnectorCode,
                x.ExternalOutletId
            }).IsUnique();

            b.HasQueryFilter(x =>
                !IsPartnerFilterEnabled ||
                CurrentPartnerId == null ||
                x.PartnerId == CurrentPartnerId);
        });
    }
}
