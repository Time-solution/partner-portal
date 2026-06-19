namespace Zahy.Commission;

/// <summary>
/// Which order monetary field supplies the commission basis. Default is Subtotal
/// (excludes tax and delivery fee). Override on the rule only when explicitly configured.
/// </summary>
public enum CommissionBasisAmountKind
{
    Subtotal = 1,
    TotalAmount = 2,
    LineSubtotal = 3
}
