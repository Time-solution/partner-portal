namespace Zahy.OrderLedger;

/// <summary>
/// How merchandise subtotal was resolved for commission basis.
/// Unavailable means neither explicit subtotal nor line items — TotalAmount must not be used.
/// </summary>
public enum OrderSubtotalResolution
{
    Explicit = 1,
    FromLines = 2,
    Unavailable = 3
}
