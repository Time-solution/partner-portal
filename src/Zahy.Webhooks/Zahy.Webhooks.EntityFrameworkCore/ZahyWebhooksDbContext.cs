using Microsoft.EntityFrameworkCore;
using Volo.Abp.Data;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore.Modeling;
using Zahy.Identity.Partners;

namespace Zahy.Webhooks;

[ConnectionStringName("Default")]
public class ZahyWebhooksDbContext : AbpDbContext<ZahyWebhooksDbContext>
{
    public DbSet<WebhookSubscription> WebhookSubscriptions { get; set; }

    public DbSet<WebhookOutboxMessage> WebhookOutboxMessages { get; set; }

    public DbSet<WebhookDelivery> WebhookDeliveries { get; set; }

    public DbSet<WebhookDeadLetter> WebhookDeadLetters { get; set; }

    public ZahyWebhooksDbContext(DbContextOptions<ZahyWebhooksDbContext> options)
        : base(options)
    {
    }

    protected virtual bool IsPartnerFilterEnabled => DataFilter.IsEnabled<IWebhookPartnerDataFilter>();

    protected virtual Guid? CurrentPartnerId =>
        LazyServiceProvider.LazyGetService<ICurrentPartner>()?.Id;

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<WebhookSubscription>(b =>
        {
            b.ToTable("WhkSubscriptions");
            b.ConfigureByConvention();

            b.Property(x => x.TargetUrl).IsRequired().HasMaxLength(WebhookConsts.MaxTargetUrlLength);
            b.Property(x => x.SigningSecret).IsRequired().HasMaxLength(WebhookConsts.MaxSigningSecretLength);
            b.Property(x => x.EventTypesJson).IsRequired();
            b.Property(x => x.FilterRulesJson);
            b.Property(x => x.Status).IsRequired();

            b.HasIndex(x => x.PartnerId);
            b.HasIndex(x => new { x.PartnerId, x.TargetUrl });

            b.HasQueryFilter(x =>
                !IsPartnerFilterEnabled ||
                CurrentPartnerId == null ||
                x.PartnerId == CurrentPartnerId);
        });

        builder.Entity<WebhookOutboxMessage>(b =>
        {
            b.ToTable("WhkOutboxMessages");
            b.ConfigureByConvention();

            b.Property(x => x.EventType).IsRequired().HasMaxLength(WebhookConsts.MaxEventTypeLength);
            b.Property(x => x.IdempotencyKey).IsRequired().HasMaxLength(WebhookConsts.MaxIdempotencyKeyLength);
            b.Property(x => x.PayloadJson).IsRequired().HasMaxLength(WebhookConsts.MaxPayloadLength);
            b.Property(x => x.Status).IsRequired();

            b.HasIndex(x => x.IdempotencyKey).IsUnique();
            b.HasIndex(x => new { x.Status, x.ScheduledAt });
            b.HasIndex(x => x.PartnerId);

            b.HasQueryFilter(x =>
                !IsPartnerFilterEnabled ||
                CurrentPartnerId == null ||
                x.PartnerId == CurrentPartnerId);
        });

        builder.Entity<WebhookDelivery>(b =>
        {
            b.ToTable("WhkDeliveries");
            b.ConfigureByConvention();

            b.Property(x => x.ResponseBodySnippet).HasMaxLength(WebhookConsts.MaxResponseSnippetLength);
            b.Property(x => x.ErrorCode).HasMaxLength(128);
            b.Property(x => x.Status).IsRequired();

            b.HasIndex(x => x.OutboxMessageId);
            b.HasIndex(x => x.PartnerId);
            b.HasIndex(x => x.AttemptedAt);

            b.HasQueryFilter(x =>
                !IsPartnerFilterEnabled ||
                CurrentPartnerId == null ||
                x.PartnerId == CurrentPartnerId);
        });

        builder.Entity<WebhookDeadLetter>(b =>
        {
            b.ToTable("WhkDeadLetters");
            b.ConfigureByConvention();

            b.Property(x => x.EventType).IsRequired().HasMaxLength(WebhookConsts.MaxEventTypeLength);
            b.Property(x => x.PayloadJson).IsRequired().HasMaxLength(WebhookConsts.MaxPayloadLength);
            b.Property(x => x.Reason).IsRequired().HasMaxLength(WebhookConsts.MaxReasonLength);

            b.HasIndex(x => x.OutboxMessageId).IsUnique();
            b.HasIndex(x => x.PartnerId);

            b.HasQueryFilter(x =>
                !IsPartnerFilterEnabled ||
                CurrentPartnerId == null ||
                x.PartnerId == CurrentPartnerId);
        });
    }
}
