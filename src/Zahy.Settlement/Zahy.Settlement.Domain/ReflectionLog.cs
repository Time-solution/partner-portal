using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities;

namespace Zahy.Settlement;

/// <summary>
/// A NON-posting record of a reflection-only order (e.g. HungerStation/noon). It produces no
/// financial journal — it exists purely for dashboard / POS visibility (order ref, gross, merchant).
/// Gross is stored as amount + currency (always VAT-inclusive) and surfaced as <see cref="Gross"/>.
/// </summary>
public class ReflectionLog : AggregateRoot<Guid>
{
    public string OrderReference { get; private set; } = string.Empty;

    public decimal GrossAmount { get; private set; }

    public string Currency { get; private set; } = SettlementConsts.DefaultCurrency;

    public Guid MerchantId { get; private set; }

    public DateTime CreatedAt { get; private set; }

    /// <summary>Merchant's own menu price (display only — NO financial impact).</summary>
    public decimal? MenuPrice { get; private set; }

    /// <summary>Price listed on the partner's app/marketplace (display only).</summary>
    public decimal? PartnerListPrice { get; private set; }

    /// <summary>Delivery fee on the partner app (display only).</summary>
    public decimal? DeliveryFee { get; private set; }

    /// <summary>Total the customer paid on the partner app (display only).</summary>
    public decimal? CustomerPaid { get; private set; }

    public Money Gross => Money.Of(GrossAmount, Currency, vatInclusive: true);

    protected ReflectionLog()
    {
    }

    public ReflectionLog(Guid id, string orderReference, Money gross, Guid merchantId, DateTime createdAt)
        : base(id)
    {
        OrderReference = Check.NotNullOrWhiteSpace(
            orderReference, nameof(orderReference), SettlementReflectionLogConsts.MaxOrderReferenceLength).Trim();
        Check.NotNull(gross, nameof(gross));
        GrossAmount = gross.Amount;
        Currency = gross.Currency;
        MerchantId = merchantId;
        CreatedAt = createdAt;
    }

    /// <summary>
    /// Attach the DISPLAY-ONLY transaction breakdown (menu → list → delivery → customer paid).
    /// These are pure visibility fields — they contribute to NO financial report and post no journal.
    /// </summary>
    public ReflectionLog WithDisplayDetail(
        decimal? menuPrice,
        decimal? partnerListPrice,
        decimal? deliveryFee,
        decimal? customerPaid)
    {
        MenuPrice = menuPrice;
        PartnerListPrice = partnerListPrice;
        DeliveryFee = deliveryFee;
        CustomerPaid = customerPaid;
        return this;
    }
}
