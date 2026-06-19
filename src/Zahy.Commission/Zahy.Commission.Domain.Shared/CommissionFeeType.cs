namespace Zahy.Commission;

/// <summary>
/// Distinct fee dimension. Only one active rule wins per fee type per order event;
/// different fee types may both accrue on the same order.
/// </summary>
public enum CommissionFeeType
{
    Sale = 1,
    Shipment = 2,
    Service = 3,
    Subscription = 4,
    Activation = 5
}
