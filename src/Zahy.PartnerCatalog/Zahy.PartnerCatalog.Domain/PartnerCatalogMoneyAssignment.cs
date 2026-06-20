using Volo.Abp;
using Zahy.Settlement;

namespace Zahy.PartnerCatalog;

internal static class PartnerCatalogMoneyAssignment
{
    internal static void Assign(
        Money money,
        Action<decimal> setAmount,
        Action<string> setCurrency,
        Action<bool> setVatInclusive,
        string invalidMoneyErrorCode = PartnerCatalogErrorCodes.InvalidPartnerCost)
    {
        if (!money.VatInclusive)
        {
            throw new BusinessException(invalidMoneyErrorCode)
                .WithData("Reason", "PartnerCatalogMoneyMustBeVatInclusive");
        }

        if (!money.IsPositive)
        {
            throw new BusinessException(invalidMoneyErrorCode)
                .WithData("Reason", "PartnerCatalogMoneyMustBePositive");
        }

        setAmount(money.Amount);
        setCurrency(money.Currency);
        setVatInclusive(money.VatInclusive);
    }

    internal static Money Read(decimal amount, string currency, bool vatInclusive) =>
        Money.Of(amount, currency, vatInclusive);
}
