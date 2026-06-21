using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities;

namespace Zahy.Settlement;

/// <summary>
/// Per-activation fee matrix — PERSISTED CONFIG ONLY. Two independently toggleable lines
/// (each may be ON at once and bill a different side): a recurring monthly Subscription fee
/// and a PerTransaction fee. Holding this row NEVER posts a journal — fee posting is gated by
/// <c>SettlementEngineOptions.PostingEnabled</c> (OFF). The lines are flattened into scalar
/// columns and surfaced as <see cref="ActivationFeeLine"/> value objects (never stored as such),
/// mirroring the frontend mock <c>ActivationFeeConfig</c> { subscription, perTransaction }.
/// </summary>
public class ActivationFeeConfig : AggregateRoot<Guid>
{
    /// <summary>The merchant activation this fee matrix configures (one config per activation).</summary>
    public Guid ActivationId { get; private set; }

    public bool SubscriptionEnabled { get; private set; }

    public decimal SubscriptionAmount { get; private set; }

    public string SubscriptionCurrency { get; private set; } = SettlementConsts.DefaultCurrency;

    public ActivationFeePayer SubscriptionPayer { get; private set; }

    public bool PerTransactionEnabled { get; private set; }

    public decimal PerTransactionAmount { get; private set; }

    public string PerTransactionCurrency { get; private set; } = SettlementConsts.DefaultCurrency;

    public ActivationFeePayer PerTransactionPayer { get; private set; }

    /// <summary>Recurring monthly subscription fee (derived view over the scalar columns).</summary>
    public ActivationFeeLine Subscription =>
        ActivationFeeLine.Of(
            SubscriptionEnabled,
            Money.Of(SubscriptionAmount, SubscriptionCurrency, vatInclusive: true),
            SubscriptionPayer);

    /// <summary>Per successful transaction fee (derived view over the scalar columns).</summary>
    public ActivationFeeLine PerTransaction =>
        ActivationFeeLine.Of(
            PerTransactionEnabled,
            Money.Of(PerTransactionAmount, PerTransactionCurrency, vatInclusive: true),
            PerTransactionPayer);

    protected ActivationFeeConfig()
    {
    }

    public ActivationFeeConfig(
        Guid id,
        Guid activationId,
        ActivationFeeLine subscription,
        ActivationFeeLine perTransaction)
        : base(id)
    {
        ActivationId = activationId;
        SetSubscription(subscription);
        SetPerTransaction(perTransaction);
    }

    public void SetSubscription(ActivationFeeLine line)
    {
        Check.NotNull(line, nameof(line));
        SubscriptionEnabled = line.Enabled;
        SubscriptionAmount = line.AmountInclusive.Amount;
        SubscriptionCurrency = line.AmountInclusive.Currency;
        SubscriptionPayer = line.Payer;
    }

    public void SetPerTransaction(ActivationFeeLine line)
    {
        Check.NotNull(line, nameof(line));
        PerTransactionEnabled = line.Enabled;
        PerTransactionAmount = line.AmountInclusive.Amount;
        PerTransactionCurrency = line.AmountInclusive.Currency;
        PerTransactionPayer = line.Payer;
    }
}
