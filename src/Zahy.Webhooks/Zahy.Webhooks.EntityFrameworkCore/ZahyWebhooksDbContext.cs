using Microsoft.EntityFrameworkCore;
using Volo.Abp.Data;
using Volo.Abp.EntityFrameworkCore;

namespace Zahy.Webhooks;

[ConnectionStringName("Default")]
public class ZahyWebhooksDbContext : AbpDbContext<ZahyWebhooksDbContext>
{
    public ZahyWebhooksDbContext(DbContextOptions<ZahyWebhooksDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        // Entity mappings for the Zahy.Webhooks module go here.
    }
}