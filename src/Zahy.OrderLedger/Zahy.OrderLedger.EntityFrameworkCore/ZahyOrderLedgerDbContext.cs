using Microsoft.EntityFrameworkCore;
using Volo.Abp.Data;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace Zahy.OrderLedger;

[ConnectionStringName("Default")]
public class ZahyOrderLedgerDbContext : AbpDbContext<ZahyOrderLedgerDbContext>
{
    public DbSet<OrderRecord> OrderRecords { get; set; }

    public DbSet<OrderLine> OrderLines { get; set; }

    public ZahyOrderLedgerDbContext(DbContextOptions<ZahyOrderLedgerDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<OrderRecord>(b =>
        {
            b.ToTable("OlgOrderRecords");
            b.ConfigureByConvention();

            b.Property(x => x.SourceSystem).IsRequired().HasMaxLength(OrderLedgerConsts.MaxSourceSystemLength);
            b.Property(x => x.SourceOrderId).IsRequired().HasMaxLength(OrderLedgerConsts.MaxSourceOrderIdLength);
            b.Property(x => x.Currency).IsRequired().HasMaxLength(3);
            b.Property(x => x.TotalAmount).HasPrecision(18, 2);
            b.Property(x => x.Subtotal).HasPrecision(18, 2);
            b.Property(x => x.SubtotalResolution).IsRequired();
            b.Property(x => x.TaxAmount).HasPrecision(18, 2);
            b.Property(x => x.DeliveryFee).HasPrecision(18, 2);
            b.Property(x => x.Direction).IsRequired();
            b.Property(x => x.Status).IsRequired();
            b.Property(x => x.PaymentStatus).IsRequired();
            b.Property(x => x.CapturedAt).IsRequired();
            b.Property(x => x.SourceTimestamp).IsRequired();

            b.HasIndex(x => new { x.SourceSystem, x.SourceOrderId, x.SourceVersion }).IsUnique();
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => x.PartnerId);

            b.HasMany(x => x.Lines).WithOne().HasForeignKey(x => x.OrderRecordId);
        });

        builder.Entity<OrderLine>(b =>
        {
            b.ToTable("OlgOrderLines");
            b.ConfigureByConvention();
            b.Property(x => x.Sku).IsRequired().HasMaxLength(OrderLedgerConsts.MaxSkuLength);
            b.Property(x => x.ProductName).IsRequired().HasMaxLength(OrderLedgerConsts.MaxProductNameLength);
            b.Property(x => x.Quantity).HasPrecision(18, 4);
            b.Property(x => x.UnitPrice).HasPrecision(18, 2);
            b.Property(x => x.LineTotal).HasPrecision(18, 2);
            b.HasIndex(x => x.OrderRecordId);
        });
    }
}
