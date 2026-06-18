using Microsoft.EntityFrameworkCore;
using Volo.Abp.Data;
using Volo.Abp.EntityFrameworkCore;

namespace Zahy.OrderLedger;

[ConnectionStringName("Default")]
public class ZahyOrderLedgerDbContext : AbpDbContext<ZahyOrderLedgerDbContext>
{
    public ZahyOrderLedgerDbContext(DbContextOptions<ZahyOrderLedgerDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        // Entity mappings for the Zahy.OrderLedger module go here.
    }
}