namespace Zahy.Commission;

/// <summary>Explicit billing account target — never infer from optional TenantId alone.</summary>
public enum BillingChargeTarget
{
    Partner = 1,
    Merchant = 2
}
