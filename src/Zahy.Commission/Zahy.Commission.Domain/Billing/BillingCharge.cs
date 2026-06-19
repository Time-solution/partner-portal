using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities;

namespace Zahy.Commission;

/// <summary>
/// Append-only billing charge row. Amounts are immutable after insert.
/// </summary>
public class BillingCharge : AggregateRoot<Guid>
{
    public Guid PartnerId { get; private set; }

    public Guid? TenantId { get; private set; }

    public BillingChargeTarget ChargeTarget { get; private set; }

    public BillingChargeKind Kind { get; private set; }

    public decimal Amount { get; private set; }

    public string Currency { get; private set; } = CommissionConsts.DefaultCurrency;

    public string IdempotencyKey { get; private set; } = string.Empty;

    public string? PeriodKey { get; private set; }

    public Guid? CommissionLedgerEntryId { get; private set; }

    public string? Description { get; private set; }

    public DateTime ChargedAt { get; private set; }

    protected BillingCharge()
    {
    }

    public static BillingCharge Create(
        Guid id,
        Guid partnerId,
        BillingChargeTarget chargeTarget,
        Guid? tenantId,
        BillingChargeKind kind,
        decimal amount,
        string idempotencyKey,
        DateTime chargedAt,
        string currency = CommissionConsts.DefaultCurrency,
        string? periodKey = null,
        Guid? commissionLedgerEntryId = null,
        string? description = null)
    {
        ValidateAmount(amount);
        ValidateIdempotencyKey(idempotencyKey);
        ValidateTarget(chargeTarget, partnerId, tenantId);

        return new BillingCharge
        {
            Id = id,
            PartnerId = partnerId,
            ChargeTarget = chargeTarget,
            TenantId = tenantId,
            Kind = kind,
            Amount = amount,
            Currency = NormalizeCurrency(currency),
            IdempotencyKey = idempotencyKey.Trim(),
            PeriodKey = periodKey?.Trim(),
            CommissionLedgerEntryId = commissionLedgerEntryId,
            Description = description?.Trim(),
            ChargedAt = chargedAt
        };
    }

    private static void ValidateTarget(BillingChargeTarget chargeTarget, Guid partnerId, Guid? tenantId)
    {
        if (partnerId == Guid.Empty)
        {
            throw new BusinessException(CommissionErrorCodes.InvalidBillingCharge)
                .WithData("Field", nameof(partnerId));
        }

        switch (chargeTarget)
        {
            case BillingChargeTarget.Partner:
                return;
            case BillingChargeTarget.Merchant:
                if (tenantId == null || tenantId == Guid.Empty)
                {
                    throw new BusinessException(CommissionErrorCodes.InvalidBillingCharge)
                        .WithData("Field", nameof(tenantId))
                        .WithData("ChargeTarget", chargeTarget.ToString());
                }

                return;
            default:
                throw new BusinessException(CommissionErrorCodes.InvalidBillingCharge)
                    .WithData("ChargeTarget", chargeTarget.ToString());
        }
    }

    private static void ValidateAmount(decimal amount)
    {
        if (amount <= 0)
        {
            throw new BusinessException(CommissionErrorCodes.InvalidBillingCharge)
                .WithData("Field", nameof(amount));
        }
    }

    private static void ValidateIdempotencyKey(string idempotencyKey)
    {
        Check.NotNullOrWhiteSpace(idempotencyKey, nameof(idempotencyKey));

        if (idempotencyKey.Length > BillingConsts.MaxIdempotencyKeyLength)
        {
            throw new BusinessException(CommissionErrorCodes.InvalidBillingCharge);
        }
    }

    private static string NormalizeCurrency(string currency)
    {
        Check.NotNullOrWhiteSpace(currency, nameof(currency));
        return currency.Trim().ToUpperInvariant();
    }
}
