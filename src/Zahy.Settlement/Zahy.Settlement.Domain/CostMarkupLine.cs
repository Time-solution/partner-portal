using System;
using Volo.Abp;

namespace Zahy.Settlement;

/// <summary>
/// A resold item priced as cost + markup: a VAT-inclusive buy price and a VAT-inclusive sell price,
/// in the same currency. The buy side is the cost; the sell side is what is charged. Margin and VAT
/// depend on the configured <see cref="VatTreatment"/>.
/// </summary>
public sealed record CostMarkupLine
{
    public Money BuyPrice { get; }

    public Money SellPrice { get; }

    private CostMarkupLine(Money buyPrice, Money sellPrice)
    {
        BuyPrice = buyPrice;
        SellPrice = sellPrice;
    }

    public string Currency => BuyPrice.Currency;

    public static CostMarkupLine Of(Money buyPrice, Money sellPrice)
    {
        if (!buyPrice.VatInclusive || !sellPrice.VatInclusive)
        {
            throw new BusinessException(SettlementVatErrorCodes.PriceMustBeVatInclusive);
        }

        if (!string.Equals(buyPrice.Currency, sellPrice.Currency, StringComparison.Ordinal))
        {
            throw new BusinessException(SettlementVatErrorCodes.PriceCurrencyMismatch)
                .WithData("Buy", buyPrice.Currency)
                .WithData("Sell", sellPrice.Currency);
        }

        return new CostMarkupLine(buyPrice, sellPrice);
    }
}
