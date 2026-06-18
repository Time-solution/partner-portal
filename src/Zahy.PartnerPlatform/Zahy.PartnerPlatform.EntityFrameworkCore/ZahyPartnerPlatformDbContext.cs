using Microsoft.EntityFrameworkCore;
using Volo.Abp.Data;
using Volo.Abp.EntityFrameworkCore;

namespace Zahy.PartnerPlatform;

[ConnectionStringName("Default")]
public class ZahyPartnerPlatformDbContext : AbpDbContext<ZahyPartnerPlatformDbContext>
{
    public ZahyPartnerPlatformDbContext(DbContextOptions<ZahyPartnerPlatformDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        // Entity mappings for the Zahy.PartnerPlatform module go here.
    }
}