namespace Zahy.PartnerCatalog;

public static class MerchantActivationStateMachine
{
    public static bool CanTransition(MerchantActivationStatus from, MerchantActivationStatus to) =>
        (from, to) switch
        {
            (MerchantActivationStatus.Pending, MerchantActivationStatus.Active) => true,
            (MerchantActivationStatus.Pending, MerchantActivationStatus.Ended) => true,
            (MerchantActivationStatus.Active, MerchantActivationStatus.Suspended) => true,
            (MerchantActivationStatus.Active, MerchantActivationStatus.Ended) => true,
            (MerchantActivationStatus.Suspended, MerchantActivationStatus.Active) => true,
            (MerchantActivationStatus.Suspended, MerchantActivationStatus.Ended) => true,
            _ => false
        };

    public static bool IsTerminal(MerchantActivationStatus status) =>
        status == MerchantActivationStatus.Ended;
}
