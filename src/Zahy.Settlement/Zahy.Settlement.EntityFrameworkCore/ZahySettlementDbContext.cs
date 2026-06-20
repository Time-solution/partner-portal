using Microsoft.EntityFrameworkCore;
using Volo.Abp.Data;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace Zahy.Settlement;

[ConnectionStringName("Default")]
public class ZahySettlementDbContext : AbpDbContext<ZahySettlementDbContext>
{
    public DbSet<SettlementCase> SettlementCases => Set<SettlementCase>();

    public DbSet<SettlementWebhookEvent> WebhookEvents => Set<SettlementWebhookEvent>();

    public ZahySettlementDbContext(DbContextOptions<ZahySettlementDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Ignore<SettlementStateTransition>();

        builder.Entity<SettlementCase>(b =>
        {
            b.ToTable("StlSettlementCases");
            b.ConfigureByConvention();

            b.Property(x => x.Book).IsRequired();
            b.Property(x => x.PartnerId).IsRequired();
            b.Property(x => x.ExternalTransactionId)
                .IsRequired()
                .HasMaxLength(SettlementCaseConsts.MaxExternalTransactionIdLength);
            b.Property(x => x.State).IsRequired();
            b.Property(x => x.CreatedAt).IsRequired();
            b.Property(x => x.UpdatedAt).IsRequired();
            b.Property(x => x.ReversesSettlementCaseId);

            // Idempotency: at most one case per (Book, ExternalTransactionId). A replayed external
            // event hits this unique index instead of creating a second case.
            b.HasIndex(x => new { x.Book, x.ExternalTransactionId }).IsUnique();

            // Derived members are not stored. The append-only transition history is persisted
            // together with the webhook event log in a later phase (C), not here.
            b.Ignore(x => x.History);
            b.Ignore("_history");
            b.Ignore(x => x.IdempotencyKey);
        });

        builder.Entity<SettlementWebhookEvent>(b =>
        {
            b.ToTable("StlSettlementWebhookEvents");
            b.ConfigureByConvention();

            b.Property(x => x.ExternalEventId).IsRequired().HasMaxLength(SettlementWebhookConsts.MaxExternalEventIdLength);
            b.Property(x => x.RawPayload).IsRequired().HasMaxLength(SettlementWebhookConsts.MaxRawPayloadLength);
            b.Property(x => x.Reason).HasMaxLength(SettlementWebhookConsts.MaxReasonLength);
            b.Property(x => x.SignatureStatus).IsRequired();
            b.Property(x => x.Outcome).IsRequired();
            b.Property(x => x.ReceivedAt).IsRequired();

            // Append-only log: indexed for explain (by case) and dedupe lookups (by book+event id);
            // NOT unique — a rejected and a later valid event for the same id may both be recorded.
            b.HasIndex(x => x.SettlementCaseId);
            b.HasIndex(x => new { x.Book, x.ExternalEventId });
        });
    }
}
