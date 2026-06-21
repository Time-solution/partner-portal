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

    public DbSet<LedgerAccount> LedgerAccounts => Set<LedgerAccount>();

    public DbSet<ReflectionLog> ReflectionLogs => Set<ReflectionLog>();

    public DbSet<ActivationFeeConfig> ActivationFeeConfigs => Set<ActivationFeeConfig>();

    public DbSet<Payment> Payments => Set<Payment>();

    public DbSet<ReconciliationBatch> ReconciliationBatches => Set<ReconciliationBatch>();

    public DbSet<Disbursement> Disbursements => Set<Disbursement>();

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

        builder.Entity<LedgerAccount>(b =>
        {
            b.ToTable("StlLedgerAccounts");
            b.ConfigureByConvention();

            b.Property(x => x.Code).IsRequired().HasMaxLength(SettlementLedgerAccountConsts.MaxCodeLength);
            b.Property(x => x.Name).IsRequired().HasMaxLength(SettlementLedgerAccountConsts.MaxNameLength);
            b.Property(x => x.Type).IsRequired();
            b.Property(x => x.NormalSide).IsRequired();
            b.Property(x => x.Portfolio).IsRequired();

            // Codes are unique across the chart.
            b.HasIndex(x => x.Code).IsUnique();
        });

        builder.Entity<ReflectionLog>(b =>
        {
            b.ToTable("StlReflectionLogs");
            b.ConfigureByConvention();

            b.Property(x => x.OrderReference).IsRequired().HasMaxLength(SettlementReflectionLogConsts.MaxOrderReferenceLength);
            b.Property(x => x.GrossAmount).IsRequired().HasColumnType("decimal(18,2)");
            b.Property(x => x.Currency).IsRequired().HasMaxLength(3);
            b.Property(x => x.MerchantId).IsRequired();
            b.Property(x => x.CreatedAt).IsRequired();

            // DISPLAY-ONLY reflection breakdown (no financial impact, all nullable).
            b.Property(x => x.MenuPrice).HasColumnType("decimal(18,2)");
            b.Property(x => x.PartnerListPrice).HasColumnType("decimal(18,2)");
            b.Property(x => x.DeliveryFee).HasColumnType("decimal(18,2)");
            b.Property(x => x.CustomerPaid).HasColumnType("decimal(18,2)");

            // Gross is a derived view over GrossAmount + Currency, never stored.
            b.Ignore(x => x.Gross);

            b.HasIndex(x => x.MerchantId);
        });

        builder.Entity<ActivationFeeConfig>(b =>
        {
            b.ToTable("StlActivationFeeConfigs");
            b.ConfigureByConvention();

            b.Property(x => x.ActivationId).IsRequired();
            b.Property(x => x.SubscriptionEnabled).IsRequired();
            b.Property(x => x.SubscriptionAmount).IsRequired().HasColumnType("decimal(18,2)");
            b.Property(x => x.SubscriptionCurrency).IsRequired().HasMaxLength(3);
            b.Property(x => x.SubscriptionPayer).IsRequired();
            b.Property(x => x.PerTransactionEnabled).IsRequired();
            b.Property(x => x.PerTransactionAmount).IsRequired().HasColumnType("decimal(18,2)");
            b.Property(x => x.PerTransactionCurrency).IsRequired().HasMaxLength(3);
            b.Property(x => x.PerTransactionPayer).IsRequired();

            // One fee matrix per activation.
            b.HasIndex(x => x.ActivationId).IsUnique();

            // The Subscription / PerTransaction lines are derived views, never stored.
            b.Ignore(x => x.Subscription);
            b.Ignore(x => x.PerTransaction);
        });

        builder.Entity<Payment>(b =>
        {
            b.ToTable("StlPayments");
            b.ConfigureByConvention();

            b.Property(x => x.AgainstRef).IsRequired().HasMaxLength(SettlementPaymentConsts.MaxAgainstRefLength);
            b.Property(x => x.Payer).IsRequired();
            b.Property(x => x.PayerId).IsRequired();
            b.Property(x => x.Amount).IsRequired().HasColumnType("decimal(18,2)");
            b.Property(x => x.Currency).IsRequired().HasMaxLength(3);
            b.Property(x => x.Date).IsRequired();
            b.Property(x => x.Method).HasMaxLength(SettlementPaymentConsts.MaxMethodLength);

            // The gross-money view over Amount + Currency is derived, never stored.
            b.Ignore(x => x.Money);

            // Several payments may target one balance — indexed for paid-to-date lookups, NOT unique.
            b.HasIndex(x => x.AgainstRef);
        });

        builder.Entity<ReconciliationBatch>(b =>
        {
            b.ToTable("StlReconciliationBatches");
            b.ConfigureByConvention();

            b.Property(x => x.PartnerId).IsRequired();
            b.Property(x => x.PeriodYear).IsRequired();
            b.Property(x => x.PeriodMonth).IsRequired();
            b.Property(x => x.State).IsRequired();
            b.Property(x => x.ReconciledAt);
            b.Property(x => x.ReconciledBy).HasMaxLength(SettlementReconciliationConsts.MaxReconciledByLength);
            b.Property(x => x.OverrideBy).HasMaxLength(SettlementReconciliationConsts.MaxReconciledByLength);
            b.Property(x => x.OverrideReason).HasMaxLength(SettlementReconciliationConsts.MaxOverrideReasonLength);

            // One batch per partner+period (all-or-nothing for the period).
            b.HasIndex(x => new { x.PartnerId, x.PeriodYear, x.PeriodMonth }).IsUnique();

            // The SettlementPeriod view is derived from the scalar columns, never stored.
            b.Ignore(x => x.Period);
            b.Ignore(x => x.IsReconciled);
        });

        builder.Entity<Disbursement>(b =>
        {
            b.ToTable("StlDisbursements");
            b.ConfigureByConvention();

            b.Property(x => x.PartnerId).IsRequired();
            b.Property(x => x.PeriodYear).IsRequired();
            b.Property(x => x.PeriodMonth).IsRequired();
            b.Property(x => x.Amount).IsRequired().HasColumnType("decimal(18,2)");
            b.Property(x => x.Currency).IsRequired().HasMaxLength(3);
            b.Property(x => x.Date).IsRequired();
            b.Property(x => x.IdempotencyKey).IsRequired().HasMaxLength(SettlementDisbursementConsts.MaxIdempotencyKeyLength);
            b.Property(x => x.State).IsRequired();
            b.Property(x => x.ReleasedBy).HasMaxLength(SettlementDisbursementConsts.MaxReleasedByLength);
            b.Property(x => x.ReleasedAt);
            b.Property(x => x.ReversalOf);

            // Several payouts may target one partner+period payable (partial allowed) — NOT unique.
            b.HasIndex(x => new { x.PartnerId, x.PeriodYear, x.PeriodMonth });

            // Double-submit safety: one row per idempotency key.
            b.HasIndex(x => x.IdempotencyKey).IsUnique();

            // Derived views are never stored.
            b.Ignore(x => x.Period);
            b.Ignore(x => x.Money);
            b.Ignore(x => x.IsReversal);
            b.Ignore(x => x.IsReleased);
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
