using Microsoft.EntityFrameworkCore;
using Volo.Abp.Data;
using Volo.Abp.EntityFrameworkCore;

namespace Zahy.Commission;

[ConnectionStringName("Default")]
public class ZahyCommissionDbContext : AbpDbContext<ZahyCommissionDbContext>
{
    public ZahyCommissionDbContext(DbContextOptions<ZahyCommissionDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        // Entity mappings for the Zahy.Commission module go here.
    }
}