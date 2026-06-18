using Microsoft.EntityFrameworkCore;
using Volo.Abp.Data;
using Volo.Abp.EntityFrameworkCore;

namespace Zahy.Identity;

[ConnectionStringName("Default")]
public class ZahyIdentityDbContext : AbpDbContext<ZahyIdentityDbContext>
{
    public ZahyIdentityDbContext(DbContextOptions<ZahyIdentityDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        // Entity mappings for the Zahy.Identity module go here.
    }
}