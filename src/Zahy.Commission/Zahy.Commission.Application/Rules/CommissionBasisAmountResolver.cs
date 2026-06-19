using Volo.Abp;

namespace Zahy.Commission;

public class CommissionBasisAmountResolver : ICommissionBasisAmountResolver
{
    public decimal Resolve(CommissionOrderAmounts orderAmounts, CommissionRuleSnapshot rule, string? productSku = null)
    {
        Check.NotNull(orderAmounts, nameof(orderAmounts));
        Check.NotNull(rule, nameof(rule));

        return rule.BasisAmountKind switch
        {
            CommissionBasisAmountKind.Subtotal => orderAmounts.Subtotal,
            CommissionBasisAmountKind.TotalAmount => orderAmounts.TotalAmount,
            CommissionBasisAmountKind.LineSubtotal => ResolveLineSubtotal(orderAmounts, productSku),
            _ => orderAmounts.Subtotal
        };
    }

    private static decimal ResolveLineSubtotal(CommissionOrderAmounts orderAmounts, string? productSku)
    {
        if (string.IsNullOrWhiteSpace(productSku) ||
            orderAmounts.LineSubtotalsBySku == null ||
            !orderAmounts.LineSubtotalsBySku.TryGetValue(productSku, out var lineSubtotal))
        {
            throw new BusinessException(CommissionErrorCodes.InvalidBasisAmount)
                .WithData("BasisAmountKind", CommissionBasisAmountKind.LineSubtotal.ToString())
                .WithData("ProductSku", productSku ?? string.Empty);
        }

        return lineSubtotal;
    }
}
