using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;

namespace Zahy.Commission;

public class PartnerBillingProfile : FullAuditedAggregateRoot<Guid>
{
    public Guid PartnerId { get; private set; }

    public decimal ActivationFeeAmount { get; private set; }

    public decimal MonthlySubscriptionAmount { get; private set; }

    public string Currency { get; private set; } = CommissionConsts.DefaultCurrency;

    public bool IsActive { get; private set; }

    protected PartnerBillingProfile()
    {
    }

    public PartnerBillingProfile(
        Guid id,
        Guid partnerId,
        decimal activationFeeAmount,
        decimal monthlySubscriptionAmount,
        string currency = CommissionConsts.DefaultCurrency)
    {
        Id = id;
        PartnerId = partnerId;
        SetAmounts(activationFeeAmount, monthlySubscriptionAmount);
        Currency = NormalizeCurrency(currency);
        IsActive = true;
    }

    public void SetAmounts(decimal activationFeeAmount, decimal monthlySubscriptionAmount)
    {
        if (activationFeeAmount < 0 || monthlySubscriptionAmount < 0)
        {
            throw new BusinessException(CommissionErrorCodes.InvalidBillingCharge);
        }

        ActivationFeeAmount = activationFeeAmount;
        MonthlySubscriptionAmount = monthlySubscriptionAmount;
    }

    private static string NormalizeCurrency(string currency)
    {
        Check.NotNullOrWhiteSpace(currency, nameof(currency));
        return currency.Trim().ToUpperInvariant();
    }
}
